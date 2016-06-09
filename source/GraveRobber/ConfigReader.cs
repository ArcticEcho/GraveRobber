/*
 * GraveRobber. A .NET PoC program for fetching data from the SOCVR graveyards.
 * Copyright © 2016, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <http://www.gnu.org/licenses/>.
 */




using System;
using System.IO;

namespace GraveRobber
{
    /// <summary>
    /// Searches through environment variables (for docker) or the configuration file 
    /// for values that help to initiate this program.
    /// </summary>
    public static class ConfigReader
    {
        /// <summary>
        /// Gets the database connection string to used to save/fetch watched questions.
        /// </summary>
        public static string DBConnection
        {
            get
            {
                return GetSetting("DB_CONNECTION", "DB Connection");
            }
        }
        /// <summary>
        /// Gets the Stack Exchange email address for the primary (chat) account
        /// to use. Important, this must be a Stack Exchange OAuth account.
        /// </summary>
        public static string AccountEmailAddressPrimary
        {
            get
            {
                return GetSetting("STACK_EXCHANGE_EMAIL_PRIMARY", "SE Email Primary");
            }
        }

        /// <summary>
        /// Gets the Stack Exchange email address for the secondary (polling) account
        /// to use. Important, this must be a Stack Exchange OAuth account.
        /// </summary>
        public static string AccountEmailAddressSecondary
        {
            get
            {
                return GetSetting("STACK_EXCHANGE_EMAIL_SECONDARY", "SE Email Secondary");
            }
        }

        /// <summary>
        /// Gets the password for the primary account to use.
        /// </summary>
        public static string AccountPasswordPrimary
        {
            get
            {
                return GetSetting("STACK_EXCHANGE_PASSWORD_PRIMARY", "SE Password Primary");
            }
        }

        /// <summary>
        /// Gets the password for the secondary account to use.
        /// </summary>
        public static string AccountPasswordSecondary
        {
            get
            {
                return GetSetting("STACK_EXCHANGE_PASSWORD_SECONDARY", "SE Password Secondary");
            }
        }

        /// <summary>
        /// The room this bot will join, listen to, and post messages to.
        /// </summary>
        public static string RoomUrl
        {
            get
            {
                return GetSetting("CHAT_ROOM_URL", "RoomURL");
            }
        }

        /// <summary>
        /// Attempts to find the config value based on the given key names.
        /// The first check will be at environment variables.
        /// The second check will be at the static settings file.
        /// If the value cannot be found in either location, a null value will be returned.
        /// </summary>
        /// <param name="enviornmentVariableName">The expected name of the environment variable.</param>
        /// <param name="settingName">The expected name of the config value in the settings file.</param>
        /// <returns>Magical data if successful, otherwise null.</returns>
        private static string GetSetting(string enviornmentVariableName, string settingName)
        {
            // First, check if the value exists in an environment variable (for docker).
            var envValue = Environment.GetEnvironmentVariable(enviornmentVariableName);

            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            // Else, check the config file.

            // Does the settings.txt file exist?
            if (!File.Exists("settings.txt"))
            {
                // File does not exist, return null for now.
                return null;
            }

            var st = settingName.ToLowerInvariant();
            var dataz = File.ReadAllLines("settings.txt");

            foreach (var line in dataz)
            {
                if (line.ToLowerInvariant().StartsWith(st))
                {
                    return line.Remove(0, line.IndexOf(":") + 1);
                }
            }

            // It's not in the config file, return null.
            return null;
        }
    }
}