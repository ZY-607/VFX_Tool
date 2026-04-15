namespace VFXTools.Editor.Analyzer
{
    public struct VolcEngineEndpointSelection
    {
        public bool useCustomModelId;
        public string customModelId;
        public string resolvedModelId;
        public bool migratedLegacyValue;
    }

    public static class VFXAnalyzerEndpointResolver
    {
        private const string KnownInvalidLegacyModelId = "d0ec0730-2ff0-4d4d-89fa-ae25e37cd23a";

        public static VolcEngineEndpointSelection ResolveVolcEngineEndpoint(
            string projectDefaultModelId,
            bool hasNewModePrefs,
            bool useCustomModelId,
            string customModelId,
            bool hasLegacyModelIdPref,
            string legacyModelId)
        {
            string normalizedProjectDefault = NormalizeProjectDefault(projectDefaultModelId);
            string normalizedCustom = Normalize(customModelId);
            string normalizedLegacy = Normalize(legacyModelId);

            if (hasNewModePrefs)
            {
                if (useCustomModelId && IsInvalidLegacyModelId(normalizedCustom))
                {
                    return BuildSelection(normalizedProjectDefault, false, string.Empty, true);
                }

                return BuildSelection(
                    normalizedProjectDefault,
                    useCustomModelId,
                    normalizedCustom,
                    false);
            }

            if (!hasLegacyModelIdPref ||
                string.IsNullOrEmpty(normalizedLegacy) ||
                normalizedLegacy == normalizedProjectDefault)
            {
                return BuildSelection(normalizedProjectDefault, false, string.Empty, false);
            }

            if (IsInvalidLegacyModelId(normalizedLegacy))
            {
                return BuildSelection(normalizedProjectDefault, false, string.Empty, true);
            }

            return BuildSelection(normalizedProjectDefault, true, normalizedLegacy, false);
        }

        public static bool IsInvalidLegacyModelId(string modelId)
        {
            return Normalize(modelId) == KnownInvalidLegacyModelId;
        }

        public static string NormalizeProjectDefault(string modelId)
        {
            string normalized = Normalize(modelId);
            return IsInvalidLegacyModelId(normalized) ? string.Empty : normalized;
        }

        private static VolcEngineEndpointSelection BuildSelection(
            string projectDefaultModelId,
            bool useCustomModelId,
            string customModelId,
            bool migratedLegacyValue)
        {
            string normalizedProjectDefault = Normalize(projectDefaultModelId);
            string normalizedCustom = useCustomModelId ? Normalize(customModelId) : string.Empty;
            string resolvedModelId = useCustomModelId ? normalizedCustom : normalizedProjectDefault;

            if (useCustomModelId && string.IsNullOrEmpty(normalizedCustom))
            {
                resolvedModelId = string.Empty;
            }

            return new VolcEngineEndpointSelection
            {
                useCustomModelId = useCustomModelId,
                customModelId = normalizedCustom,
                resolvedModelId = resolvedModelId,
                migratedLegacyValue = migratedLegacyValue
            };
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
