using System;

internal static class ExceptionExtensions
{
    extension(Exception @this)
    {
        public int? ExitCode
        {
            get
            {
                if (@this.Data.Contains("ExitCode"))
                {
                    return (int?)@this.Data["ExitCode"];
                }
                else
                {
                    return null;
                }
            }
            set
            {
                @this.Data["ExitCode"] = value;
            }
        }

        public Exception WithData(string key, object value)
        {
            @this.Data[key] = value;

            return @this;
        }
    }
}
