namespace ResponsiveWindowTool.Services
{
    public interface IPrivilegeService
    {
        bool IsAdministrator();
        void RestartAsAdministrator();
    }
}