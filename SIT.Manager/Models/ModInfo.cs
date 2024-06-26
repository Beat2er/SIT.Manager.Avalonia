﻿using System.Collections.Generic;

namespace SIT.Manager.Models;

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string SupportedVersion { get; set; } = string.Empty;
    public string ModVersion { get; set; } = string.Empty;
    public string PortVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModUrl { get; set; } = string.Empty;
    public bool RequiresFiles { get; set; }
    public string OriginalDownloadUrl { get; set; } = string.Empty;
    public List<string> PluginFiles { get; set; } = [];
    public List<string> ConfigFiles { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
}
