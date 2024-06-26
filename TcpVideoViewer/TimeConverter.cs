using System;
using System.Globalization;

namespace TcpVideoViewer
{
    public static class TimeConverter
    {
        private static readonly DateTime _dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Convert a UNIX/POSIX time given as a double to MS .NET DateTime representation
        /// </summary>
        /// <param name="millisec">Number of milliseconds since UNIX Epoch (Jan 1st 1970)</param>
        /// <returns>A DateTime representing the same time</returns>
        public static DateTime From(double millisec)
        {
            DateTime dt = _dt1970 + new TimeSpan((long)millisec * 10000);
            dt = dt.ToLocalTime();
            return dt;
        }

        /// <summary>
        /// Convert a UNIX/POSIX time given as an integer to MS .NET DateTime representation
        /// </summary>
        /// <param name="millisec">Number of milliseconds since UNIX Epoch (Jan 1st 1970)</param>
        /// <returns>A DateTime representing the same time</returns>
        public static DateTime From(int millisec)
        {
            DateTime dt = _dt1970 + new TimeSpan((long)millisec * 10000);
            dt = dt.ToLocalTime();
            return dt;
        }

        /// <summary>
        /// Convert a UNIX/POSIX time given as a string representing a number to MS .NET DateTime representation
        /// </summary>
        /// <param name="millisec">Number of milliseconds since UNIX Epoch (Jan 1st 1970)</param>
        /// <returns>A DateTime representing the same time</returns>
        public static DateTime From(string millisec)
        {
            DateTime dt = _dt1970 + new TimeSpan(long.Parse(millisec, NumberStyles.Integer) * 10000);
            dt = dt.ToLocalTime();
            return dt;
        }

        /// <summary>
        /// Convert an MS .NET DateTime to a double representing the elapsed number of milliseconds since UNIX Epoch
        /// </summary>
        /// <param name="time">A date and a time</param>
        /// <returns>Number of milliseconds since UNIX Epoch corresponding to the input time</returns>
        public static double ToDouble(DateTime time)
        {
            return (double)(time.ToUniversalTime() - _dt1970).Ticks / 10000;
        }

        /// <summary>
        /// Convert an MS .NET DateTime to an integer representing the elapsed number of milliseconds since UNIX Epoch
        /// </summary>
        /// <param name="time">A date and a time</param>
        /// <returns>Number of milliseconds since UNIX Epoch corresponding to the input time</returns>
        public static int ToInt(DateTime time)
        {
            return (int)((time.ToUniversalTime() - _dt1970).Ticks / 10000);
        }
    }
}
