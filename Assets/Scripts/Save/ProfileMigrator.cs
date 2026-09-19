using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LastGround.Save
{
    /// <summary>
    /// Upgrades an older profile.json step by step (v1 → v2 → …, TDD_02 §24) on its JSON tree before it is read into
    /// <see cref="ProfileData"/>. A step is registered for the version it upgrades from and must set nothing but the
    /// fields it changes; the migrator writes the new version number.
    /// </summary>
    public sealed class ProfileMigrator
    {
        readonly SortedDictionary<int, Action<JObject>> _steps = new SortedDictionary<int, Action<JObject>>();

        /// <summary>The shipped chain (none yet: v1 is the first profile format, M9).</summary>
        public static ProfileMigrator Default => new ProfileMigrator();

        public ProfileMigrator Add(int fromVersion, Action<JObject> step)
        {
            _steps[fromVersion] = step;
            return this;
        }

        /// <summary>Result of a migration.</summary>
        public enum Outcome
        {
            Current,
            Migrated,
            /// <summary>Written by a newer game version: read what is understood, but never overwrite it.</summary>
            TooNew,
            /// <summary>A step is missing: the file cannot be upgraded.</summary>
            Unsupported,
        }

        public Outcome Migrate(JObject json, int target)
        {
            int version = json.Value<int?>("Version") ?? 1;
            if (version > target) return Outcome.TooNew;
            if (version == target) return Outcome.Current;
            while (version < target)
            {
                if (!_steps.TryGetValue(version, out Action<JObject> step)) return Outcome.Unsupported;
                step(json);
                version++;
                json["Version"] = version;
            }
            return Outcome.Migrated;
        }
    }
}
