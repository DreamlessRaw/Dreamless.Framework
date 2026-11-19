using Dreamless.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Dreamless.Infrastructure.Services;

public sealed class MessageService(ISqlSugarClient db, ICurrentUser current, ILocalizationService l10n, IHubContext<NotifyHub> hub) : IMessageService
{
    public async Task<long> SendAsync(MessageSendRequest request)
    {
        var title = request.Title.Trim();
        var content = request.Content.Trim();
        if (title.Length is < 1 or > 120)
        {
            throw new BusinessException(await l10n.T("error.message.title", "请填写 120 字以内的标题"));
        }

        if (content.Length is < 1 or > 2000)
        {
            throw new BusinessException(await l10n.T("error.message.content", "请填写 2000 字以内的内容"));
        }

        if (current.TenantId <= 0)
        {
            throw new BusinessException(await l10n.T("error.message.tenant", "当前账号没有租户，不能发送通知"));
        }

        var users = await Targets(request);
        if (users.Count == 0)
        {
            throw new BusinessException(await l10n.T("error.message.empty", "没有可通知的用户"));
        }

        var sender = await db.Queryable<SysUser>().FirstAsync(u => u.Id == current.UserId);
        var message = new SysMessage
        {
            TenantId = current.TenantId,
            Title = title,
            Content = content,
            Scope = request.Scope,
            DeptId = request.Scope == MessageScope.Dept ? current.DeptId : null,
            SenderId = current.UserId,
            SenderName = string.IsNullOrWhiteSpace(sender?.NickName) ? current.UserName : sender!.NickName
        };
        var id = await db.Insertable(message).ExecuteReturnBigIdentityAsync();
        var saved = await db.Queryable<SysMessage>().InSingleAsync(id) ?? message;
        await db.Insertable(users.Select(user => new SysMessageUser
        {
            TenantId = current.TenantId,
            MessageId = id,
            UserId = user.Id
        }).ToList()).ExecuteCommandAsync();

        var notice = new MessageNoticeDto
        {
            Id = id,
            Title = saved.Title,
            Content = saved.Content,
            Scope = saved.Scope,
            SenderId = saved.SenderId,
            SenderName = saved.SenderName,
            CreateTime = saved.CreateTime == default ? DateTime.Now : saved.CreateTime
        };
        var group = request.Scope switch
        {
            MessageScope.Dept => NotifyGroups.Dept(current.TenantId, current.DeptId!.Value),
            MessageScope.Users => null,
            _ => NotifyGroups.Tenant(current.TenantId)
        };
        if (group != null)
        {
            await hub.Clients.Group(group).SendAsync("Receive", notice);
        }
        else
        {
            await hub.Clients.Groups(users.Select(user => NotifyGroups.User(current.TenantId, user.Id)).ToList()).SendAsync("Receive", notice);
        }

        return id;
    }

    public async Task<PageResult<MessageInboxDto>> InboxAsync(MessageInboxQuery query)
    {
        var q = db.Queryable<SysMessageUser>()
            .InnerJoin<SysMessage>((box, message) => box.MessageId == message.Id)
            .Where((box, message) => box.UserId == current.UserId)
            .WhereIF(query.Unread == true, (box, message) => box.IsRead == false);
        var total = await q.CountAsync();
        var items = await q.OrderBy((box, message) => message.CreateTime, OrderByType.Desc)
            .Select((box, message) => new MessageInboxDto
            {
                Id = box.Id,
                MessageId = message.Id,
                Title = message.Title,
                Content = message.Content,
                Scope = message.Scope,
                SenderName = message.SenderName,
                IsRead = box.IsRead,
                CreateTime = message.CreateTime,
                ReadTime = box.ReadTime
            })
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        return new PageResult<MessageInboxDto> { Total = total, Items = items };
    }

    public async Task<PageResult<MessageSentDto>> SentAsync(PageQuery query)
    {
        var q = db.Queryable<SysMessage>().Where(message => message.SenderId == current.UserId);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(message => message.CreateTime, OrderByType.Desc)
            .Skip((query.NormalizedPage - 1) * query.NormalizedSize)
            .Take(query.NormalizedSize)
            .ToListAsync();
        var ids = rows.Select(row => row.Id).ToList();
        var counts = ids.Count == 0
            ? new Dictionary<long, int>()
            : (await db.Queryable<SysMessageUser>().Where(box => ids.Contains(box.MessageId)).Select(box => box.MessageId).ToListAsync())
                .GroupBy(id => id)
                .ToDictionary(group => group.Key, group => group.Count());
        return new PageResult<MessageSentDto>
        {
            Total = total,
            Items = rows.Select(row => new MessageSentDto
            {
                Id = row.Id,
                Title = row.Title,
                Content = row.Content,
                Scope = row.Scope,
                RecipientCount = counts.GetValueOrDefault(row.Id),
                CreateTime = row.CreateTime
            }).ToList()
        };
    }

    public Task<int> UnreadCountAsync() =>
        db.Queryable<SysMessageUser>().Where(box => box.UserId == current.UserId && box.IsRead == false).CountAsync();

    public async Task ReadAsync(long messageId)
    {
        var rows = await db.Queryable<SysMessageUser>()
            .Where(box => box.MessageId == messageId && box.UserId == current.UserId && box.IsRead == false)
            .ToListAsync();
        if (rows.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var row in rows)
        {
            row.IsRead = true;
            row.ReadTime = now;
        }

        await db.Updateable(rows).UpdateColumns(box => new { box.IsRead, box.ReadTime }).ExecuteCommandAsync();
    }

    public async Task ReadAllAsync()
    {
        var rows = await db.Queryable<SysMessageUser>()
            .Where(box => box.UserId == current.UserId && box.IsRead == false)
            .ToListAsync();
        if (rows.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var row in rows)
        {
            row.IsRead = true;
            row.ReadTime = now;
        }

        await db.Updateable(rows).UpdateColumns(box => new { box.IsRead, box.ReadTime }).ExecuteCommandAsync();
    }

    public async Task DeleteInboxAsync(long id)
    {
        var row = await db.Queryable<SysMessageUser>().FirstAsync(box => box.Id == id && box.UserId == current.UserId)
            ?? throw new BusinessException(await l10n.T("error.notFound", "数据不存在"), 404);
        row.IsDeleted = true;
        await db.Updateable(row).UpdateColumns(box => new { box.IsDeleted, box.UpdateTime, box.UpdateUserId }).ExecuteCommandAsync();
    }

    public async Task<List<MessageUserOptionDto>> RecipientsAsync(string? keyword)
    {
        var q = db.Queryable<SysUser>().Where(user => user.Status == CommonStatus.Enabled);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            q = q.Where(user => user.UserName.Contains(text) || user.NickName.Contains(text));
        }

        var users = await q.OrderBy(user => user.Id).Take(50).ToListAsync();
        var deptIds = users.Where(user => user.DeptId != null).Select(user => user.DeptId!.Value).Distinct().ToList();
        var depts = deptIds.Count == 0
            ? new Dictionary<long, string>()
            : (await db.Queryable<SysDept>().Where(dept => deptIds.Contains(dept.Id)).ToListAsync()).ToDictionary(dept => dept.Id, dept => dept.Name);
        return users.Select(user => new MessageUserOptionDto
        {
            Id = user.Id,
            UserName = user.UserName,
            NickName = user.NickName,
            DeptName = user.DeptId != null && depts.TryGetValue(user.DeptId.Value, out var name) ? name : null
        }).ToList();
    }

    private async Task<List<SysUser>> Targets(MessageSendRequest request)
    {
        var q = db.Queryable<SysUser>().Where(user => user.Status == CommonStatus.Enabled);
        if (request.Scope == MessageScope.Dept)
        {
            if (current.DeptId is null or 0)
            {
                throw new BusinessException(await l10n.T("error.message.dept", "当前账号没有部门，不能按部门通知"));
            }

            return await q.Where(user => user.DeptId == current.DeptId).ToListAsync();
        }

        if (request.Scope == MessageScope.Users)
        {
            var ids = request.UserIds.Where(id => id > 0).Distinct().Take(200).ToList();
            if (ids.Count == 0)
            {
                throw new BusinessException(await l10n.T("error.message.users", "请选择要通知的用户"));
            }

            var users = await q.Where(user => ids.Contains(user.Id)).ToListAsync();
            if (users.Count != ids.Count)
            {
                throw new BusinessException(await l10n.T("error.message.user", "有用户不存在或已停用"));
            }

            return users;
        }

        return await q.ToListAsync();
    }
}
