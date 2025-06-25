#region License

//The contents of this file are subject to the Mozilla Public License
//Version 1.1 (the "License"); you may not use this file except in
//compliance with the License. You may obtain a copy of the License at
//http://www.mozilla.org/MPL/
//Software distributed under the License is distributed on an "AS IS"
//basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
//License for the specific language governing rights and limitations
//under the License.

#endregion

using System;

namespace Migrator.Framework
{
    /// <summary>
    /// Describe a migration
    /// </summary>
    public class MigrationAttribute : Attribute
    {
        private long _version;
        private bool _ignore = false;

        public string Scope { get; set; }

        /// <summary>
        /// Describe the migration
        /// </summary>
        /// <param name="version">The unique version of the migration.</param>	
        public MigrationAttribute(long version)
        {
            Version = version;
        }
        public MigrationAttribute(int year, int month, int day, int hour, int minute, int second)
        {
            var combined = String.Format("{0:D4}{1:D2}{2:D2}{3:D2}{4:D2}{5:D2}", year, month, day, hour, minute, second);
            Version = long.Parse(combined);
        }
        /// <summary>
        /// The version reflected by the migration
        /// </summary>
        public long Version
        {
            get { return _version; }
            private set { _version = value; }
        }

        /// <summary>
        /// Set to <c>true</c> to ignore this migration.
        /// </summary>
        public bool Ignore
        {
            get { return _ignore; }
            set { _ignore = value; }
        }
    }
}
