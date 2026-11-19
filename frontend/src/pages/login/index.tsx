import { LoginForm, ProFormText } from '@ant-design/pro-components';
import { Space, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/auth/AuthContext';
import { FontSizeTool, LanguageTool, ModeTool, ThemeTool } from '@/components/AppearanceTools';
import i18n from '@/i18n';

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  return (
    <div className="login-page">
      <LoginForm
        title={t('app.name')}
        subTitle={t('app.subtitle')}
        submitter={{ searchConfig: { submitText: t('login.submit') } }}
        onFinish={async (values) => {
          await login(values as { tenantCode: string; userName: string; password: string });
          navigate('/dashboard');
        }}
        actions={
          <Space size={4} className="login-tools">
            <ThemeTool />
            <ModeTool />
            <FontSizeTool />
            <LanguageTool onChange={async (language) => {
              localStorage.setItem('dreamless-lang', language);
              await i18n.changeLanguage(language);
            }} />
          </Space>
        }
      >
        <ProFormText name="tenantCode" label={t('login.tenant')} initialValue="default" rules={[{ required: true }]} />
        <ProFormText name="userName" label={t('login.username')} rules={[{ required: true }]} />
        <ProFormText.Password name="password" label={t('login.password')} rules={[{ required: true }]} />
        <Typography.Paragraph type="secondary">{t('login.hint')}</Typography.Paragraph>
      </LoginForm>
    </div>
  );
}
