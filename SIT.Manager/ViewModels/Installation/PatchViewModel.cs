﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SIT.Manager.Controls;
using SIT.Manager.Interfaces;
using SIT.Manager.Models.Installation;
using SIT.Manager.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SIT.Manager.ViewModels.Installation;

public partial class PatchViewModel : InstallationViewModelBase
{
    private readonly IFileService _fileService;
    private readonly IInstallerService _installerService;
    private readonly ILogger<PatchViewModel> _logger;

    private static readonly Dictionary<int, string> _patcherResultMessages = new() {
        { 0, "Patcher was closed." },
        { 10, "Patcher was successful." },
        { 11, "Could not find 'EscapeFromTarkov.exe'." },
        { 12, "'Aki_Patches' is missing." },
        { 13, "Install folder is missing a file." },
        { 14, "Install folder is missing a folder." },
        { 15, "Patcher failed." }
    };

    private readonly Progress<double> _copyProgress = new();
    private readonly Progress<double> _downloadProgress = new();
    private readonly Progress<double> _extractionProgress = new();

    [ObservableProperty]
    private double _copyProgressPercentage = 0;

    [ObservableProperty]
    private double _downloadProgressPercentage = 0;

    [ObservableProperty]
    private double _extractionProgressPercentage = 0;

    [ObservableProperty]
    private bool _requiresPatching = false;

    [ObservableProperty]
    private bool _hasPatcherError = false;

    [ObservableProperty]
    private EmbeddedProcessWindow? _embeddedPatcherWindow;

    public PatchViewModel(IFileService fileService, IInstallerService installerService, ILogger<PatchViewModel> logger) : base()
    {
        _fileService = fileService;
        _installerService = installerService;
        _logger = logger;

        RequiresPatching = !string.IsNullOrEmpty(CurrentInstallProcessState.DownloadMirrorUrl);

        _copyProgress.ProgressChanged += CopyProgress_ProgressChanged;
        _downloadProgress.ProgressChanged += DownloadProgress_ProgressChanged;
        _extractionProgress.ProgressChanged += ExtractionProgress_ProgressChanged;
    }

    private void CopyProgress_ProgressChanged(object? sender, double e)
    {
        CopyProgressPercentage = e;
    }

    private void DownloadProgress_ProgressChanged(object? sender, double e)
    {
        DownloadProgressPercentage = e * 100;
    }

    private void ExtractionProgress_ProgressChanged(object? sender, double e)
    {
        ExtractionProgressPercentage = e;
    }

    private async Task RunPatcher()
    {
        string[] files = Directory.GetFiles(CurrentInstallProcessState.EftInstallPath, "Patcher.exe", new EnumerationOptions() { MatchCasing = MatchCasing.CaseInsensitive, MaxRecursionDepth = 0 });
        if (files.Length == 0)
        {
            _logger.LogError("Patcher.exe not found in install directory even though one should exist already");
            HasPatcherError = true;
            return;
        }
        string patcherPath = files[0];

        EmbeddedProcessWindow embeddedProcessWindow = new(_installerService.CreatePatcherProcess(patcherPath));
        await embeddedProcessWindow.StartProcess();

        // TODO get window embedding working on linux - maybe we just add the patcher as a git module? and then embed the window of the patcher itself?
        // Window embedding only seems to work properly on Windows 
        // More info here: https://github.com/AvaloniaUI/Avalonia/issues/10354 and https://github.com/AvaloniaUI/Avalonia/issues/6124
        // So we will only do this on Windows for now until we spend some time to get it working nicely on Linux.
        if (OperatingSystem.IsWindows())
        {
            EmbeddedPatcherWindow = embeddedProcessWindow;
            await EmbeddedPatcherWindow.WaitForExit();
        }
        else
        {
            await embeddedProcessWindow.StartProcess();
        }

        _patcherResultMessages.TryGetValue(embeddedProcessWindow.ExitCode, out string? patcherResult);
        _logger.LogInformation($"RunPatcher: {patcherResult}");

        int exitCode = embeddedProcessWindow.ExitCode;
        EmbeddedPatcherWindow = null;

        // Success exit code
        if (exitCode == 10)
        {
            if (File.Exists(patcherPath))
            {
                File.Delete(patcherPath);
            }

            string patcherLog = Path.Combine(CurrentInstallProcessState.EftInstallPath, "Patcher.log");
            if (File.Exists(patcherLog))
            {
                File.Delete(patcherLog);
            }

            string akiPatchesDir = Path.Combine(CurrentInstallProcessState.EftInstallPath, "Aki_Patches");
            if (Directory.Exists(akiPatchesDir))
            {
                Directory.Delete(akiPatchesDir, true);
            }

            ProgressInstall();
        }
        else
        {
            HasPatcherError = true;
        }
    }

    [RelayCommand]
    private void EndInstall()
    {
        RegressInstall();
    }

    protected override async void OnActivated()
    {
        base.OnActivated();

        Messenger.Send(new InstallationRunningMessage(true));

        await DownloadAndRunPatcher();
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        Messenger.Send(new InstallationRunningMessage(false));
    }

    public async Task DownloadAndRunPatcher()
    {
        if (CurrentInstallProcessState.UsingBsgInstallPath)
        {
            await _fileService.CopyDirectory(CurrentInstallProcessState.BsgInstallPath, CurrentInstallProcessState.EftInstallPath, _copyProgress);
        }

        if (RequiresPatching)
        {
            await _installerService.DownloadAndExtractPatcher(CurrentInstallProcessState.DownloadMirrorUrl, CurrentInstallProcessState.EftInstallPath, _downloadProgress, _extractionProgress);

            await RunPatcher();
        }
        else
        {
            ProgressInstall();
        }
    }
}
