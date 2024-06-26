using System;
using System.Globalization;

namespace ServerCommandWrapper
{
    /// <summary>
    /// Authentication types: Basic, Windows, or Windows default
    /// </summary>
    public enum AuthenticationType
    {
        Basic,
        Windows,
        WindowsDefault
    }

    /// <summary>
    /// A simple representation of the LoginInfo classes in ServerCommandService_CServer
    /// </summary>
    public class LoginInfo
    {
        public DateTime RegistrationTimeField;
        public TimeSpan TimeToLive;
        public String Token;

        /// <summary>
        /// Converts a ServerCommandService_CServer.LoginInfo into a the shared type of LoginInfo
        /// </summary>
        /// <param name="loginInfo">LoginInfo from the C server</param>
        /// <returns>A shared type of LoginInfo</returns>
        public static LoginInfo CreateFrom(ServerCommandService.LoginInfo loginInfo)
        {
            if (loginInfo == null)
                return null;

            LoginInfo lInfo = new LoginInfo()
            {
                RegistrationTimeField = loginInfo.RegistrationTime,
                TimeToLive = TimeSpan.FromMilliseconds(loginInfo.TimeToLive.MicroSeconds / 1000),
                Token = loginInfo.Token
            };
            return lInfo;

        }
    }

    public static class Constants
    {
        public const string ManufacturerName = "Sample Manufacturer";
        public static readonly string InstanceId = Environment.MachineName.GetHashCode().ToString(CultureInfo.InvariantCulture);
    }
}
