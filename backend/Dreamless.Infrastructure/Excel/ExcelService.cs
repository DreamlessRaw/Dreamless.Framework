using Magicodes.ExporterAndImporter.Core;
using Magicodes.ExporterAndImporter.Excel;

namespace Dreamless.Infrastructure.Excel;

public sealed class ExcelService(IFieldAccessService fields, ILocalizationService localization, ICurrentUser current) : IExcelService
{
    public async Task<byte[]> ExportAsync<T>(IEnumerable<T> rows, string pageCode) where T : class, new()
    {
        using (ExcelAmbient.Push(await StateAsync(pageCode, forExport: true)))
        {
            IExporter exporter = new ExcelExporter();
            return await exporter.ExportAsByteArray(rows.ToList());
        }
    }

    public async Task<byte[]> TemplateAsync<T>(string pageCode) where T : class, new()
    {
        using (ExcelAmbient.Push(await StateAsync(pageCode, forExport: false)))
        {
            IImporter importer = new ExcelImporter();
            return await importer.GenerateTemplateBytes<T>();
        }
    }

    public async Task<ExcelReadResult<T>> ReadAsync<T>(Stream stream, string pageCode) where T : class, new()
    {
        using (ExcelAmbient.Push(await StateAsync(pageCode, forExport: false)))
        {
            IImporter importer = new ExcelImporter();
            var result = await importer.Import<T>(stream);
            var errors = new List<string>();
            if (result.Exception != null)
            {
                errors.Add(result.Exception.Message);
            }

            if (result.TemplateErrors != null)
            {
                errors.AddRange(result.TemplateErrors.Select(AppMap.DescribeError));
            }

            if (result.RowErrors != null)
            {
                errors.AddRange(result.RowErrors.Select(AppMap.DescribeError));
            }

            return new ExcelReadResult<T>
            {
                Rows = errors.Count == 0 ? result.Data?.ToList() ?? [] : [],
                Errors = errors
            };
        }
    }

    private async Task<ExcelAmbientState> StateAsync(string pageCode, bool forExport)
    {
        var access = await fields.GetAsync(pageCode);
        var denied = access
            .Where(x => forExport ? !x.CanExport : !x.CanEdit)
            .Select(x => x.Field);
        var labels = await localization.GetMapAsync(string.IsNullOrWhiteSpace(current.Culture) ? "zh-CN" : current.Culture);
        return new ExcelAmbientState
        {
            Labels = new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase),
            DeniedFields = denied.ToHashSet(StringComparer.OrdinalIgnoreCase)
        };
    }
}
