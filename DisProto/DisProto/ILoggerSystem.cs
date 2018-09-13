
namespace Core
{
    public interface ILoggerSystem
    {
        void LogInfo(object msg);
        void LogInfo(object fmt, params object[] param);

        void LogWarn(object msg);
        void LogWarn(object msg, params object[] param);

        void LogErro(object msg);
        void LogErro(object msg, params object[] param);

        void Assert(bool cond);
        void Assert(bool cond, object msg);
        void Assert(bool cond, object fmt, params object[] param);

        System.Action<string> LogCallback { get; set; }
    }
}
