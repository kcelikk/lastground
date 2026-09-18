using System.Xml;
using UnityEditor.Android;

namespace LastGround.EditorTools.Build
{
    /// <summary>
    /// Adds the permissions LAN discovery needs to the generated manifest (TDD_02 §30.1). INTERNET is forced by
    /// Player Settings; location and nearby-device permissions are intentionally absent.
    /// </summary>
    public sealed class AndroidPermissions : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        static readonly string[] Required =
        {
            "android.permission.INTERNET",
            "android.permission.ACCESS_NETWORK_STATE",
            "android.permission.ACCESS_WIFI_STATE",
            "android.permission.CHANGE_WIFI_MULTICAST_STATE",
            "android.permission.VIBRATE",
        };

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = System.IO.Path.Combine(path, "src", "main", "AndroidManifest.xml");
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            XmlElement manifest = doc.DocumentElement;
            if (manifest == null) return;

            foreach (string permission in Required)
            {
                if (HasPermission(manifest, permission)) continue;
                XmlElement element = doc.CreateElement("uses-permission");
                element.SetAttribute("name", AndroidNs, permission);
                manifest.PrependChild(element);
            }
            doc.Save(manifestPath);
        }

        static bool HasPermission(XmlElement manifest, string permission)
        {
            foreach (XmlNode node in manifest.ChildNodes)
            {
                if (node is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == permission)
                    return true;
            }
            return false;
        }
    }
}
