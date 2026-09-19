using System;
using System.Collections.Generic;
using System.Xml.Linq;
using NUnit.Framework;

namespace LastGround.Tests
{
    /// <summary>
    /// Guards Assets/link.xml: JSON data classes must survive IL2CPP stripping (their constructors are only
    /// reached through reflection). Fails when a listed type is renamed or a known JSON type is missing.
    /// </summary>
    public class LinkXmlTests
    {
        static readonly Type[] JsonTypes =
        {
            typeof(LastGround.Localization.LanguageCatalog),
            typeof(LastGround.Localization.LanguageInfo),
            typeof(LastGround.Save.SettingsData),
            typeof(LastGround.Save.ProfileData),
            typeof(LastGround.Save.ProfileStats),
            typeof(LastGround.Save.OutfitChoice),
        };

        static Dictionary<string, string> LoadPreserved()
        {
            var preserved = new Dictionary<string, string>(StringComparer.Ordinal);
            XDocument doc = XDocument.Load("Assets/link.xml");
            foreach (XElement assembly in doc.Root.Elements("assembly"))
            foreach (XElement type in assembly.Elements("type"))
                preserved[type.Attribute("fullname").Value] = assembly.Attribute("fullname").Value;
            return preserved;
        }

        [Test]
        public void JsonTypes_ArePreserved()
        {
            Dictionary<string, string> preserved = LoadPreserved();
            foreach (Type type in JsonTypes)
            {
                Assert.IsTrue(preserved.TryGetValue(type.FullName, out string assembly), type.FullName + " missing in link.xml");
                Assert.AreEqual(type.Assembly.GetName().Name, assembly, type.FullName + " has wrong assembly in link.xml");
            }
        }

        [Test]
        public void PreservedTypes_Exist()
        {
            foreach (var pair in LoadPreserved())
                Assert.IsNotNull(Type.GetType(pair.Key + ", " + pair.Value), "link.xml lists unknown type " + pair.Key);
        }
    }
}
