using System;
using System.Collections.Generic;

namespace LogLevels
{
    public class LogLevel : IEquatable<LogLevel>
    {
        private static Dictionary<string, byte> levels = new Dictionary<string, byte>(){
            {"Debug", 1},
            {"Info", 2}
        };

        private LogLevel(string name, byte level) { Name = name; Level = level; }

        public string Name { get; set; }
        public int Level { get; set; }

        public static LogLevel Debug { get { return new LogLevel("Debug", levels["Debug"]); } }
        public static LogLevel Info { get { return new LogLevel("Info", levels["Info"]); } }

        public static LogLevel getLogLevelByName(string name)
        {
            LogLevel result = null;
            if(levels.ContainsKey(name)) 
                result = new LogLevel(name, levels[name]);

            return result;
        }

        public static string[] getNames()
        {
            string[] keys = new string[levels.Keys.Count];
            levels.Keys.CopyTo(keys, 0);
            return keys;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as LogLevel);
        }

        public bool Equals(LogLevel p)
        {
            // If parameter is null, return false.
            if (Object.ReferenceEquals(p, null))
            {
                return false;
            }

            // Optimization for a common success case.
            if (Object.ReferenceEquals(this, p))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (this.GetType() != p.GetType())
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return (Level == p.Level);
        }

        public override int GetHashCode()
        {
            int hashCode = 1635173235;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + Level.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(LogLevel lhs, LogLevel rhs)
        {
            if (Object.ReferenceEquals(lhs, null))
            {
                if (Object.ReferenceEquals(rhs, null))
                {
                    // null == null = true.
                    return true;
                }

                // Only the left side is null.
                return false;
            }

            return lhs.Level.Equals(rhs.Level);
        }

        public static bool operator !=(LogLevel lhs, LogLevel rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator >(LogLevel lhs, LogLevel rhs)
        {
            if (lhs == rhs)
                return false;

            if (Object.ReferenceEquals(lhs, null))
                return false;
            
            if (Object.ReferenceEquals(rhs, null))
                return false;

            return lhs.Level > rhs.Level;
        }

        public static bool operator <(LogLevel lhs, LogLevel rhs)
        {
            if (lhs == rhs)
                return false;

            if (Object.ReferenceEquals(lhs, null))
                return false;

            if (Object.ReferenceEquals(rhs, null))
                return false;


            return lhs.Level < rhs.Level;
        }

        public static bool operator >=(LogLevel lhs, LogLevel rhs)
        {
            if (lhs == rhs)
                return true;

            if (Object.ReferenceEquals(lhs, null))
                return false;

            if (Object.ReferenceEquals(rhs, null))
                return false;


            return lhs.Level >= rhs.Level;
        }

        public static bool operator <=(LogLevel lhs, LogLevel rhs)
        {
            if (lhs == rhs)
                return true;

            if (Object.ReferenceEquals(lhs, null))
                return false;

            if (Object.ReferenceEquals(rhs, null))
                return false;


            return lhs.Level <= rhs.Level;
        }

    }
}