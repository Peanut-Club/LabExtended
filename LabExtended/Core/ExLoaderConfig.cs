using System.ComponentModel;

using LabExtended.Core.Configs;

namespace LabExtended.Core
{
    /// <summary>
    /// Represents the loader's config.
    /// </summary>
    public class ExLoaderConfig
    {
        [Description("Logging configuration.")]
        public LogConfig Logging { get; set; } = new LogConfig();

        [Description("Hook configuration.")]
        public HookConfig Hooks { get; set; } = new HookConfig();

        [Description("Voice chat configuration.")]
        public VoiceConfig Voice { get; set; } = new VoiceConfig();
    }
}