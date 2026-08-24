using System;
using UnityEngine;

namespace MirraCloud
{
    [CreateAssetMenu(menuName = "Mirra Cloud/Developer Settings", fileName = "DeveloperSettings", order = 1)]
    public class DeveloperSettings : ScriptableObject
    {
        public EnvironmentProfile[] Environments;
        public int SelectedEnvironment;

        private const string RESOURCES_PATH = "DeveloperSettings";

        public EnvironmentProfile ActiveProfile
        {
            get
            {
                if (SelectedEnvironment <= 0)
                    return null;

                var index = SelectedEnvironment - 1;
                if (Environments == null || index >= Environments.Length)
                    return null;

                return Environments[index];
            }
        }

        public static DeveloperSettings TryLoad()
        {
            return Resources.Load<DeveloperSettings>(RESOURCES_PATH);
        }
    }

    [Serializable]
    public class EnvironmentProfile
    {
        public string Name;
        public string SdkUrl;
        public string EditorUrl;
    }
}
