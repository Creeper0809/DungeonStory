// Standalone .NET Framework launcher, never imported into the Unity project.
// No SwitchDesktop, SetForegroundWindow, SendInput or interactive fallback.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

internal static class IsolatedUnityLauncher
{
    private const uint AllStationAccess = 0x37f, AllDesktopAccess = 0x1ff;
    private static string output;
    private static bool ownsOutput;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-test") return SelfTest();
            if (args.Length == 4 && args[0] == "--child-probe")
                return ChildProbe(args[1], args[2], args[3]);
            bool probeOnly = args.Length == 2 && args[0] == "--probe";
            bool unity = (args.Length == 6 || args.Length == 7) && args[0] == "--unity";
            Require(probeOnly || unity, "Expected --probe OUTPUT or --unity EXE PROJECT METHOD OUTPUT MODE [serial-import-job1].");
            output = FullPath(args[probeOnly ? 1 : 4]);
            Require(!Directory.Exists(output) && !File.Exists(output), "Create-only evidence directory already exists.");
            string exe = unity ? FullPath(args[1]) : null;
            string project = unity ? FullPath(args[2]) : null;
            string method = unity ? args[3] : null;
            string mode = unity ? args[5] : null;
            string startupProfile = unity && args.Length == 7 ? args[6] : null;
            string[] preparedUnityArguments = null;
            if (unity)
            {
                Require(File.Exists(exe) && string.Equals(Path.GetFileName(exe), "Unity.exe", StringComparison.OrdinalIgnoreCase), "Unity executable is required.");
                Require(Directory.Exists(Path.Combine(project, "Assets")) && Directory.Exists(Path.Combine(project, "ProjectSettings")), "Actual Unity project is required.");
                Require(output.StartsWith(Path.Combine(project, "Artifacts", "QA") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Evidence must be inside project Artifacts/QA.");
                Require(System.Text.RegularExpressions.Regex.IsMatch(method, @"^[A-Za-z_][A-Za-z0-9_.]+$"), "Invalid execute method.");
                Require(mode == "edit" || mode == "play" || mode == "campaign", "Unknown verification mode.");
                preparedUnityArguments = BuildUnityArguments(project, method, output, mode, startupProfile);
                // This process and its owned children only. Serialized builds
                // bound compiler fan-out without reducing the memory safety floor.
                if (startupProfile != null) Environment.SetEnvironmentVariable("BEE_BUILD_THREADS", "1");
                if (method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch"
                    || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch"
                    || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch")
                    Require(FullCaptureReportBindingIsValid(mode,
                        Environment.GetEnvironmentVariable("DUNGEONSTORY_U0_E11_CAPTURE_REPORT_PATH"), output),
                        "Full captivity/wildlife lifecycle requires PlayMode and its exact scoped report binding.");
                Require(mode == "edit" || IsOwnedPlayEntry(method), "Only an owned PlayMode entry may omit quit.");
                Require(Process.GetProcessesByName("Unity").Length == 0, "An existing Unity process must not be overlapped.");
                foreach (string pending in new[]
                {
                    "Temp/playmode-persistence-snapshots",
                    "Temp/full-world-round-trip-playmode.request",
                    "Temp/full-world-round-trip-playmode-console.buffer",
                    "Temp/captivity-wildlife-lifecycle-playmode.flag",
                    "Temp/wim029-companion-role-outcome.flag",
                    "Temp/wim-048-resident-room-evacuation.flag",
                    "Temp/phase80-infrastructure-command-playmode.request"
                })
                    Require(!File.Exists(Path.Combine(project, pending)) && !Directory.Exists(Path.Combine(project, pending)), "Preserve existing verification state: " + pending);
            }
            Directory.CreateDirectory(output);
            ownsOutput = true;
            using (var isolated = new IsolatedStation())
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;
                string proof = Path.Combine(output, "isolation-probe.txt");
                int probeExit = isolated.Run(self, new[] { "--child-probe", proof, isolated.StationName, isolated.DesktopName }, output, 20);
                Require(probeExit == 0 && File.Exists(proof) && File.ReadAllText(proof).Contains("PASS_NONINTERACTIVE_PROBE"), "Noninteractive child probe failed; Unity was not started.");
                WriteNew(Path.Combine(output, "launcher-context.txt"), "station=" + isolated.StationName + "\ndesktop=" + isolated.DesktopName
                    + "\nvisible=false\njobUiRestrictions=255\nkillChildrenOnClose=true\ninteractiveFallback=false\nstartupResourceProfile="
                    + (startupProfile ?? "default") + "\nbeeBuildThreads="
                    + (Environment.GetEnvironmentVariable("BEE_BUILD_THREADS") ?? "default") + "\n");
                if (probeOnly) return 0;
                string unityEvidence = Path.Combine(output, "unity-evidence");
                Environment.SetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT", unityEvidence);
                Environment.SetEnvironmentVariable("DUNGEONSTORY_QA_PERSISTENCE_ROOT", mode == "edit" ? null : Path.Combine(unityEvidence, "persistent-data"));
                Environment.SetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE", mode == "campaign" ? "1" : null);
                bool fullSkillReplay = mode == "campaign"
                    && method == "DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch"
                    && Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_SKILL_REPLAY") == "1";
                // Only this measured whole-chain replay needs its 1800s game
                // guard plus startup/compile allowance; other runs stay at 900s.
                int code = isolated.Run(exe, preparedUnityArguments, project, fullSkillReplay ? 1920 : 900);
                WriteNew(Path.Combine(output, "launcher-exit.txt"), "unityExit=" + code + "\nnoninteractiveStation=true\n");
                return code;
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (ownsOutput && output != null && Directory.Exists(output))
            {
                string failure = Path.Combine(output, "launcher-failure.txt");
                if (!File.Exists(failure)) WriteNew(failure, error.ToString());
            }
            return 1;
        }
    }

    private static int ChildProbe(string proof, string expectedStation, string expectedDesktop)
    {
        IntPtr station = Native.GetProcessWindowStation();
        string stationName = ObjectName(station);
        string desktopName = ObjectName(Native.GetThreadDesktop(Native.GetCurrentThreadId()));
        Require(stationName == expectedStation && stationName != "WinSta0" && !IsVisible(station), "Child did not enter the noninteractive window station.");
        Require(desktopName == expectedDesktop, "Child desktop binding differs from launcher.");
        bool inJob;
        Check(Native.IsProcessInJob(Native.GetCurrentProcess(), IntPtr.Zero, out inJob), "IsProcessInJob");
        Require(inJob, "Probe is not contained in a job.");
        // Test a real window only AFTER OS identity proves this station cannot
        // display UI or receive user input. Never activate a user's window.
        IntPtr window = Native.CreateWindowExW(0, "STATIC", "Noninteractive QA probe", 0x10000000,
            0, 0, 32, 32, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Check(window != IntPtr.Zero, "CreateWindowEx probe");
        Check(Native.DestroyWindow(window), "DestroyWindow probe");
        Native.MemoryStatus memory = ReadMemoryStatus();
        WriteNew(proof, "PASS_NONINTERACTIVE_PROBE\nstation=" + stationName + "\ndesktop=" + desktopName
            + "\nvisible=false\nwindowCreatedAndDestroyed=true\ninJob=true\navailablePhysicalBytes="
            + memory.AvailablePhysical + "\navailableCommitBytes=" + memory.AvailablePageFile + "\n");
        return 0;
    }

    private sealed class IsolatedStation : IDisposable
    {
        private IntPtr station, desktop, job;
        internal string StationName, DesktopName;

        internal IsolatedStation()
        {
            IntPtr originalStation = Native.GetProcessWindowStation();
            IntPtr originalDesktop = Native.GetThreadDesktop(Native.GetCurrentThreadId());
            IntPtr descriptor = IntPtr.Zero;
            bool success = false;
            try
            {
                string sid = WindowsIdentity.GetCurrent().User.Value;
                uint descriptorLength;
                Check(Native.ConvertStringSecurityDescriptorToSecurityDescriptorW("D:P(A;;GA;;;SY)(A;;GA;;;" + sid + ")", 1, out descriptor, out descriptorLength), "Create owner-only ACL");
                var security = new Native.SecurityAttributes { Length = Marshal.SizeOf(typeof(Native.SecurityAttributes)), Descriptor = descriptor };
                // NULL requests an OS-generated noninteractive station name and
                // works without creating an account, service or requiring elevation.
                station = Native.CreateWindowStationW(null, 0, AllStationAccess, ref security);
                Check(station != IntPtr.Zero, "CreateWindowStation");
                StationName = ObjectName(station);
                Require(!string.Equals(StationName, "WinSta0", StringComparison.OrdinalIgnoreCase) && !IsVisible(station), "Refusing interactive station.");
                Check(Native.SetProcessWindowStation(station), "Set launcher station");
                DesktopName = "DungeonStoryQA_" + Guid.NewGuid().ToString("N");
                desktop = Native.CreateDesktopExW(DesktopName, null, IntPtr.Zero, 0, AllDesktopAccess, ref security, 4096, IntPtr.Zero);
                Check(desktop != IntPtr.Zero, "Create isolated desktop");
                job = Native.CreateJobObjectW(IntPtr.Zero, null);
                Check(job != IntPtr.Zero, "Create owned job");
                var limits = new Native.ExtendedLimit { Basic = new Native.BasicLimit { LimitFlags = 0x2000 } };
                SetJobInfo(job, 9, limits); // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                SetJobInfo(job, 4, new Native.UiRestrictions { Flags = 0xff });
                success = true;
            }
            finally
            {
                bool restored = Native.SetProcessWindowStation(originalStation);
                bool threadRestored = Native.SetThreadDesktop(originalDesktop);
                if (descriptor != IntPtr.Zero) Native.LocalFree(descriptor);
                if (!success || !restored || !threadRestored) Dispose();
                Check(restored && threadRestored, "Restore launcher's original station/desktop");
            }
        }

        internal int Run(string executable, string[] arguments, string workingDirectory, int timeoutSeconds)
        {
            bool unityChild = arguments.Contains("-projectPath");
            if (unityChild) RequireMemoryHeadroom(ReadMemoryStatus(), starting: true);
            var startup = new Native.StartupInfo
            {
                Size = Marshal.SizeOf(typeof(Native.StartupInfo)), Desktop = StationName + "\\" + DesktopName,
                Flags = 0x81, ShowWindow = 0 // hidden and startup cursor feedback disabled
            };
            Native.ProcessInformation process;
            var command = new StringBuilder(Quote(executable) + " " + string.Join(" ", arguments.Select(Quote)));
            Check(Native.CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                0x08000004 | 0x00004000, IntPtr.Zero, workingDirectory, ref startup, out process), "Create isolated suspended process");
            bool assigned = false;
            try
            {
                Check(Native.AssignProcessToJobObject(job, process.Process), "Contain child before first instruction");
                assigned = true;
                Require(Native.ResumeThread(process.Thread) != uint.MaxValue, "Resume isolated child failed.");
                Console.WriteLine("ISOLATED_PROCESS pid=" + process.ProcessId + "; station=" + StationName + "; desktop=" + DesktopName);
                var timer = Stopwatch.StartNew();
                while (true)
                {
                    uint wait = Native.WaitForSingleObject(process.Process, 250);
                    if (wait == 0) break;
                    Require(wait == 258, "Child wait failed.");
                    Require(timer.Elapsed.TotalSeconds < timeoutSeconds, "Isolated child exceeded bounded execution time.");
                    if (unityChild) RequireMemoryHeadroom(ReadMemoryStatus(), starting: false);
                }
                uint code;
                Check(Native.GetExitCodeProcess(process.Process, out code), "Read child exit code");
                return unchecked((int)code);
            }
            catch
            {
                if (assigned) Native.TerminateJobObject(job, 1);
                else Native.TerminateProcess(process.Process, 1); // exact suspended child only
                Native.WaitForSingleObject(process.Process, 5000);
                throw;
            }
            finally
            {
                Native.CloseHandle(process.Thread);
                Native.CloseHandle(process.Process);
            }
        }

        public void Dispose()
        {
            if (job != IntPtr.Zero) { Native.CloseHandle(job); job = IntPtr.Zero; }
            if (desktop != IntPtr.Zero) { Native.CloseDesktop(desktop); desktop = IntPtr.Zero; }
            if (station != IntPtr.Zero) { Native.CloseWindowStation(station); station = IntPtr.Zero; }
        }
    }

    private static void SetJobInfo<T>(IntPtr job, int kind, T value) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try { Marshal.StructureToPtr(value, buffer, false); Check(Native.SetInformationJobObject(job, kind, buffer, (uint)size), "Set job restriction " + kind); }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static string ObjectName(IntPtr handle)
    {
        uint needed;
        var name = new StringBuilder(512);
        Check(Native.GetUserObjectInformationW(handle, 2, name, (uint)(name.Capacity * 2), out needed), "Read OS object identity");
        return name.ToString();
    }
    private static bool IsVisible(IntPtr station)
    {
        Native.UserFlags flags;
        uint needed;
        Check(Native.GetUserObjectFlags(station, 1, out flags, (uint)Marshal.SizeOf(typeof(Native.UserFlags)), out needed), "Read station visibility");
        return (flags.Flags & 1) != 0;
    }
    private static string FullPath(string path)
    {
        Require(Path.IsPathRooted(path), "Absolute path required.");
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        for (var cursor = new DirectoryInfo(Directory.Exists(full) ? full : Path.GetDirectoryName(full)); cursor != null; cursor = cursor.Parent)
            Require(!cursor.Exists || (cursor.Attributes & FileAttributes.ReparsePoint) == 0, "Reparse-point path rejected.");
        return full;
    }
    private static string Quote(string value)
    {
        Require(value != null && value.IndexOf('\0') < 0 && value.IndexOf('\n') < 0 && value.IndexOf('\r') < 0, "Invalid command argument.");
        var result = new StringBuilder("\"");
        int slashes = 0;
        foreach (char ch in value)
        {
            if (ch == '\\') { slashes++; continue; }
            if (ch == '"') result.Append('\\', slashes * 2 + 1).Append(ch);
            else result.Append('\\', slashes).Append(ch);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
    private static bool IsOwnedPlayEntry(string method)
    {
        return method == "DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch"
            || method == "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch"
            || method == "GameWorldLineageDebugScenarios.RequestActualContainerForBatch"
            || method == "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch"
            || method == "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestActualContainerForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestDeprivationForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestRestockForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch"
            || method == "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch"
            || method == "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch"
            || method == "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch"
            || method == "V20CampaignDebugScenarios.RequestWim044IsolatedBatch"
            || method == "DungeonFullWorldRoundTripPlayModeFacade.RequestInjuredReturneeSaveRegression"
            || method == "DungeonFullWorldRoundTripPlayModeFacade.RequestExteriorDepartureExposureRegression"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestSkillBlueprintHaulDiagnostics"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestConstructionHaulDiagnostics"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestSkillResearchDiagnostics"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestSkillBattleDiagnostics"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestCompletedCraftDeliveryDiagnostics"
            || method == "DungeonLiveNarrativeCampaignRunner.RequestOriginalSkillPendingDiagnostics"
            || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunTransportBatch"
            || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunBatch"
            || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunFeedColdResumeBatch"
            || method == "CaptivityWildlifeLifecyclePlayModeVerifier.RunHusbandryPendingColdResumeBatch"
            || method == "Wim029CompanionRoleOutcomePlayModeVerifier.RunBatch"
            || method == "Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch"
            || method == "InfrastructureCommandOutcomePlayModeFacade.RequestRunFromMenu"
            || method == "DefenseEngagementPlayModeVerifier.StartWim048Batch";
    }
    private static bool FullCaptureReportBindingIsValid(string mode, string configuredReport, string evidenceRoot)
    {
        return mode == "play" && string.Equals(configuredReport,
            Path.Combine(evidenceRoot, "captivity-wildlife-lifecycle-report.txt"), StringComparison.OrdinalIgnoreCase);
    }
    private static string[] BuildUnityArguments(string project, string method, string evidenceRoot, string mode, string startupProfile)
    {
        Require(method != "V20CampaignDebugScenarios.RequestWim044IsolatedBatch" || mode == "play",
            "WIM044 requires its dedicated PlayMode entry.");
        Require(method != "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch" || mode == "play",
            "Composition closure requires its dedicated PlayMode entry.");
        Require(method != "GameWorldLineageDebugScenarios.RequestActualContainerForBatch" || mode == "play",
            "Game-world lineage requires its dedicated PlayMode entry.");
        Require(method != "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch" || mode == "play",
            "Character restore lifetime requires its dedicated PlayMode entry.");
        Require(method != "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch" || mode == "play",
            "Restore outcome requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestActualContainerForBatch" || mode == "play",
            "Movement ownership requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch" || mode == "play",
            "Destinationless movement requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestDeprivationForBatch" || mode == "play",
            "Deprivation movement requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch" || mode == "play",
            "Primitive movement requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestRestockForBatch" || mode == "play",
            "Restock movement requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch" || mode == "play",
            "Medical transfer requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch" || mode == "play",
            "Work accident requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch" || mode == "play",
            "Faction reinforcement requires its dedicated PlayMode entry.");
        Require(method != "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch" || mode == "play",
            "Skill targeting requires its dedicated PlayMode entry.");
        Require(method != "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch" || mode == "play",
            "Physical destructive drain requires its dedicated PlayMode entry.");
        Require(method != "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch" || mode == "play",
            "Narrative context lifetime requires its dedicated PlayMode entry.");
        Require((method != "ArchitectureRefactorVerificationBatch.RunCore"
                && method != "ArchitectureRefactorVerificationBatch.RunCampaign"
                && method != "ArchitectureRefactorVerificationBatch.RunProductionBills"
                && method != "ArchitectureRefactorVerificationBatch.RunBuildings"
                && method != "ArchitectureRefactorVerificationBatch.RunDefense"
                && method != "ArchitectureRefactorVerificationBatch.RunBuildingVisual"
                && method != "ArchitectureRefactorVerificationBatch.RunCropPersistence"
                && method != "ArchitectureRefactorVerificationBatch.RunCharacterDefinitions"
                && method != "ArchitectureRefactorVerificationBatch.RunEquipmentEvolution"
                && method != "ArchitectureRefactorVerificationBatch.RunConsumablePolicies"
                && method != "ArchitectureRefactorVerificationBatch.RunConsumableExecution") || mode == "edit",
            "Architecture verification requires its dedicated edit-mode entry.");
        Require(startupProfile == null || startupProfile == "serial-import-job1", "Unknown startup resource profile.");
        if (startupProfile != null)
            Require((mode == "edit" && (method == "FacilityShopPurchaseT01DebugScenarios.RunFromBatch"
                || method == "FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunT02FromBatch"
                || method == "EnvironmentCropProductionEntryDebugScenarios.RunCropCommandObserversFromBatch"
                || method == "EnvironmentCropProductionEntryDebugScenarios.RunCropAndFastingObserversFromBatch"
                || method == "ResearchWorkObserverDebugScenarios.RunFromBatch"
                || method == "RemainingPresentationObserverDebugScenarios.RunForBatch"
                || method == "MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch"
                || method == "ObserverCompositionClosureDebugScenarios.RunForBatch"
                || method == "PilotInputExperienceProfileExporter.RunForBatch"
                || method == "ArchitectureRefactorVerificationBatch.RunCore"
                || method == "ArchitectureRefactorVerificationBatch.RunCampaign"
                || method == "ArchitectureRefactorVerificationBatch.RunProductionBills"
                || method == "ArchitectureRefactorVerificationBatch.RunBuildings"
                || method == "ArchitectureRefactorVerificationBatch.RunDefense"
                || method == "ArchitectureRefactorVerificationBatch.RunBuildingVisual"
                || method == "ArchitectureRefactorVerificationBatch.RunCropPersistence"
                || method == "ArchitectureRefactorVerificationBatch.RunCharacterDefinitions"
                || method == "ArchitectureRefactorVerificationBatch.RunEquipmentEvolution"
                || method == "ArchitectureRefactorVerificationBatch.RunConsumablePolicies"
                || method == "ArchitectureRefactorVerificationBatch.RunConsumableExecution"
                || method == "V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch"
                || method == "MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch"))
                || (mode == "play" && (method == "DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch"
                    || method == "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch"
                    || method == "GameWorldLineageDebugScenarios.RequestActualContainerForBatch"
                    || method == "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch"
                    || method == "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestActualContainerForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestDeprivationForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestRestockForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch"
                    || method == "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch"
                    || method == "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch"
                    || method == "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch")),
                "Serialized startup requires a dedicated supported edit or owned whole-world PlayMode entry.");
        var arguments = new System.Collections.Generic.List<string>
        {
            "-batchmode", "-nographics", "-projectPath", project, "-executeMethod", method,
            "-logFile", Path.Combine(evidenceRoot, "unity.log")
        };
        if (method == "DefenseEngagementPlayModeVerifier.StartWim048Batch")
        {
            arguments.Add("-wim048Report");
            arguments.Add(Path.Combine(evidenceRoot, "resident-evacuation-ui02-report.txt"));
            arguments.Add("-wim048Exit");
        }
        if (mode == "edit") arguments.Add("-quit");
        if (startupProfile != null)
            arguments.AddRange(new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" });
        return arguments.ToArray();
    }
    private static int SelfTest()
    {
        Require(Quote("") == "\"\"", "Empty argument quoting failed.");
        Require(Quote("a b") == "\"a b\"", "Space quoting failed.");
        Require(Quote("x\\") == "\"x\\\\\"", "Trailing slash quoting failed.");
        Require(Quote("x\"y") == "\"x\\\"y\"", "Embedded quote escaping failed.");
        bool rejected = false;
        try { Quote("bad\nargument"); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Control argument accepted.");
        Require(IsOwnedPlayEntry("DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch"), "Whole-world PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("V20CampaignDebugScenarios.RequestWim044IsolatedBatch"), "WIM044 PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("V20CampaignDebugScenarios.RequestWim044IsolatedBatchUnexpected"), "Prefixed WIM044 entry accepted.");
        Require(IsOwnedPlayEntry("Wim029CompanionRoleOutcomePlayModeVerifier.RunBatch"), "Companion PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatch"), "Haul PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry(null), "Missing PlayMode entry accepted.");
        Require(!IsOwnedPlayEntry("UnknownVerifier.RunHaulBatch"), "Foreign PlayMode entry accepted.");
        Require(IsOwnedPlayEntry("ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch"), "Composition PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatchUnexpected"), "Prefixed composition entry accepted.");
        Require(IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestActualContainerForBatch"), "Game-world lineage PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch"), "Character restore lifetime PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch"), "Restore outcome PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestActualContainerForBatch"), "Movement ownership PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestDestinationlessForBatch"), "Destinationless movement PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestDeprivationForBatch"), "Deprivation movement PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestPrimitiveForBatch"), "Primitive movement PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestRestockForBatch"), "Restock movement PlayMode entry rejected.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch"), "Medical transfer PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestMedicalTransferForBatchUnexpected"), "Prefixed medical transfer entry accepted.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch"), "Work accident PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestWorkAccidentForBatchUnexpected"), "Prefixed work accident entry accepted.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch"), "Faction reinforcement PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatchUnexpected"), "Prefixed faction reinforcement entry accepted.");
        Require(IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch"), "Skill targeting PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestSkillTargetingForBatchUnexpected"), "Prefixed skill targeting entry accepted.");
        Require(IsOwnedPlayEntry("PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch"), "Physical destructive-drain PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatchUnexpected"), "Prefixed physical destructive-drain entry accepted.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestRestockForBatchUnexpected"), "Prefixed restock movement entry accepted.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestDestinationlessForBatchUnexpected"), "Prefixed destinationless movement entry accepted.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestDeprivationForBatchUnexpected"), "Prefixed deprivation movement entry accepted.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestPrimitiveForBatchUnexpected"), "Prefixed primitive movement entry accepted.");
        Require(!IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestActualContainerForBatchUnexpected"), "Prefixed game-world lineage entry accepted.");
        Require(!IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatchUnexpected"), "Prefixed character restore lifetime entry accepted.");
        Require(!IsOwnedPlayEntry("GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatchUnexpected"), "Prefixed restore outcome entry accepted.");
        Require(!IsOwnedPlayEntry("MovementOwnershipDebugScenarios.RequestActualContainerForBatchUnexpected"), "Prefixed movement ownership entry accepted.");
        Require(IsOwnedPlayEntry("NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch"), "Narrative context lifetime PlayMode entry rejected.");
        Require(!IsOwnedPlayEntry("NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatchUnexpected"), "Prefixed narrative context lifetime entry accepted.");
        Require(!IsOwnedPlayEntry("Wim029CompanionRoleOutcomePlayModeVerifier.RunHaulBatchUnexpected"), "Prefixed PlayMode entry accepted.");
        const string captureRoot = @"C:\DungeonStory-QA";
        string captureReport = Path.Combine(captureRoot, "captivity-wildlife-lifecycle-report.txt");
        Require(FullCaptureReportBindingIsValid("play", captureReport, captureRoot), "Full capture report binding rejected.");
        Require(FullCaptureReportBindingIsValid("play", captureReport.ToUpperInvariant(), captureRoot), "Windows report case binding rejected.");
        Require(!FullCaptureReportBindingIsValid("edit", captureReport, captureRoot), "Full capture EditMode accepted.");
        Require(!FullCaptureReportBindingIsValid("campaign", captureReport, captureRoot), "Full capture campaign mode accepted.");
        Require(!FullCaptureReportBindingIsValid("play", null, captureRoot), "Full capture missing report binding accepted.");
        Require(!FullCaptureReportBindingIsValid("play", Path.Combine(captureRoot, "wildlife-capture-release-report.txt"), captureRoot), "Full capture transport report accepted.");
        Require(!FullCaptureReportBindingIsValid("play", captureReport, captureRoot + "-other"), "Full capture foreign evidence root accepted.");
        const string shopEntry = "FacilityShopPurchaseT01DebugScenarios.RunFromBatch";
        string[] defaultShopArguments = BuildUnityArguments(captureRoot, shopEntry, captureRoot, "edit", null);
        Require(defaultShopArguments.SequenceEqual(new[] { "-batchmode", "-nographics", "-projectPath", captureRoot,
            "-executeMethod", shopEntry, "-logFile", Path.Combine(captureRoot, "unity.log"), "-quit" }),
            "Default Unity arguments changed.");
        foreach (string entry in new[] { shopEntry, "FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunT02FromBatch",
            "EnvironmentCropProductionEntryDebugScenarios.RunCropCommandObserversFromBatch",
            "EnvironmentCropProductionEntryDebugScenarios.RunCropAndFastingObserversFromBatch",
            "ResearchWorkObserverDebugScenarios.RunFromBatch",
            "RemainingPresentationObserverDebugScenarios.RunForBatch",
            "MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch",
            "ObserverCompositionClosureDebugScenarios.RunForBatch",
            "PilotInputExperienceProfileExporter.RunForBatch",
            "ArchitectureRefactorVerificationBatch.RunCore",
            "ArchitectureRefactorVerificationBatch.RunCampaign",
            "ArchitectureRefactorVerificationBatch.RunProductionBills",
            "ArchitectureRefactorVerificationBatch.RunBuildings",
            "ArchitectureRefactorVerificationBatch.RunDefense",
            "ArchitectureRefactorVerificationBatch.RunBuildingVisual",
            "ArchitectureRefactorVerificationBatch.RunCropPersistence",
            "ArchitectureRefactorVerificationBatch.RunCharacterDefinitions",
            "ArchitectureRefactorVerificationBatch.RunEquipmentEvolution",
            "ArchitectureRefactorVerificationBatch.RunConsumablePolicies",
            "ArchitectureRefactorVerificationBatch.RunConsumableExecution",
            "MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch",
            "V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch" })
            Require(BuildUnityArguments(captureRoot, entry, captureRoot, "edit", "serial-import-job1")
                .Skip(9).SequenceEqual(new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
                "Serialized startup arguments differ.");
        Require(BuildUnityArguments(captureRoot, "DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized PlayMode startup arguments differ.");
        Require(BuildUnityArguments(captureRoot, "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized composition PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "GameWorldLineageDebugScenarios.RequestActualContainerForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized game-world lineage PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized character restore lifetime PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized restore outcome PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestActualContainerForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized movement ownership PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized destinationless movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestDeprivationForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized deprivation movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized primitive movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestRestockForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized restock movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized medical transfer movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized work accident movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized faction reinforcement movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized skill targeting movement PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized physical destructive-drain PlayMode arguments differ.");
        Require(BuildUnityArguments(captureRoot, "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch",
            captureRoot, "play", "serial-import-job1").Skip(8).SequenceEqual(
                new[] { "-refreshImportMode", "InProcess", "-job-worker-count", "1" }),
            "Serialized narrative context lifetime PlayMode arguments differ.");
        foreach (string[] invalid in new[]
        {
            new[] { "ArchitectureRefactorVerificationBatch.RunCore", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunCore", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCoreUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCampaign", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunCampaign", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCampaignUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunProductionBills", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunProductionBills", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunProductionBillsUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildings", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildings", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildingsUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunDefense", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunDefense", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunDefenseUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildingVisual", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildingVisual", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunBuildingVisualUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCropPersistence", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunCropPersistence", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCropPersistenceUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCharacterDefinitions", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunCharacterDefinitions", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunCharacterDefinitionsUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunEquipmentEvolution", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunEquipmentEvolution", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunEquipmentEvolutionUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumablePolicies", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumablePolicies", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumablePoliciesUnexpected", "edit", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumableExecution", "play", null },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumableExecution", "play", "serial-import-job1" },
            new[] { "ArchitectureRefactorVerificationBatch.RunConsumableExecutionUnexpected", "edit", "serial-import-job1" },
            new[] { shopEntry, "play", "serial-import-job1" },
            new[] { "Unknown.Run", "edit", "serial-import-job1" },
            new[] { "MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch", "play", "serial-import-job1" },
            new[] { "MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatchUnexpected", "edit", "serial-import-job1" },
            new[] { "V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatch", "play", "serial-import-job1" },
            new[] { "V25NarrativeInferenceDebugScenarios.RunSkillNarrativeEvidenceBatchUnexpected", "edit", "serial-import-job1" },
            new[] { "RemainingPresentationObserverDebugScenarios.RunForBatch", "play", "serial-import-job1" },
            new[] { "RemainingPresentationObserverDebugScenarios.RunForBatchUnexpected", "edit", "serial-import-job1" },
            new[] { "MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatch", "play", "serial-import-job1" },
            new[] { "MigratedProducerRunOffenseEntryE2EDebugScenarios.RunRelationshipObservationBatchUnexpected", "edit", "serial-import-job1" },
            new[] { "ObserverCompositionClosureDebugScenarios.RunForBatch", "play", "serial-import-job1" },
            new[] { "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch", "edit", "serial-import-job1" },
            new[] { "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatch", "campaign", "serial-import-job1" },
            new[] { "ObserverCompositionClosureDebugScenarios.RequestActualContainerForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestActualContainerForBatch", "edit", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch", "edit", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestActualContainerForBatch", "edit", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestActualContainerForBatch", "campaign", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatch", "campaign", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestActualContainerForBatch", "campaign", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestActualContainerForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestCharacterRestoreLifetimeForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "GameWorldLineageDebugScenarios.RequestRestoreOutcomeForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestActualContainerForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDestinationlessForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDestinationlessForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDeprivationForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDeprivationForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestDeprivationForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestPrimitiveForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestPrimitiveForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestRestockForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestRestockForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestRestockForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestMedicalTransferForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestWorkAccidentForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestFactionReinforcementForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch", "edit", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatch", "campaign", "serial-import-job1" },
            new[] { "MovementOwnershipDebugScenarios.RequestSkillTargetingForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch", "edit", null },
            new[] { "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch", "campaign", null },
            new[] { "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch", "edit", "serial-import-job1" },
            new[] { "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatch", "campaign", "serial-import-job1" },
            new[] { "PhysicalItemLogisticsPlayModeVerifier.RequestDestructiveDrainPreparedOutputForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch", "edit", "serial-import-job1" },
            new[] { "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatch", "campaign", "serial-import-job1" },
            new[] { "NarrativeContextLifetimeDebugScenarios.RequestActualContainerForBatchUnexpected", "play", "serial-import-job1" },
            new[] { "DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch", "campaign", "serial-import-job1" },
            new[] { shopEntry, "edit", "serial-import-job1-extra" },
            new[] { "V20CampaignDebugScenarios.RequestWim044IsolatedBatch", "edit", null },
            new[] { "V20CampaignDebugScenarios.RequestWim044IsolatedBatch", "campaign", null }
        })
        {
            rejected = false;
            try { BuildUnityArguments(captureRoot, invalid[0], captureRoot, invalid[1], invalid[2]); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Unsupported startup profile or entry accepted.");
        }
        Require(Marshal.SizeOf(typeof(Native.StartupInfo)) == (IntPtr.Size == 8 ? 104 : 68), "STARTUPINFO layout mismatch.");
        Require(Marshal.SizeOf(typeof(Native.ExtendedLimit)) == (IntPtr.Size == 8 ? 144 : 112), "Job limit layout mismatch.");
        Require(Marshal.SizeOf(typeof(Native.MemoryStatus)) == 64, "Memory status layout mismatch.");
        const ulong gib = 1024UL * 1024 * 1024;
        Require(HasMemoryHeadroom(2 * gib, 2 * gib, true), "Startup memory boundary rejected.");
        Require(!HasMemoryHeadroom(2 * gib - 1, 2 * gib, true), "Low startup physical memory accepted.");
        Require(HasMemoryHeadroom(gib, 2 * gib, false), "Runtime memory boundary rejected.");
        Require(!HasMemoryHeadroom(gib - 1, 2 * gib, false), "Low runtime physical memory accepted.");
        Require(!HasMemoryHeadroom(4 * gib, 2 * gib - 1, false), "Low commit headroom accepted.");
        Console.WriteLine("PASS pure quoting/layout, owned PlayMode entry allowlist, full capture report binding, startup resource profile and memory-headroom policy checks; no desktop or process created.");
        return 0;
    }
    private static void WriteNew(string path, string text)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
    }
    private static bool HasMemoryHeadroom(ulong physical, ulong commit, bool starting)
    {
        const ulong gib = 1024UL * 1024 * 1024;
        return physical >= (starting ? 2 * gib : gib) && commit >= 2 * gib;
    }
    private static Native.MemoryStatus ReadMemoryStatus()
    {
        var memory = new Native.MemoryStatus { Length = 64 };
        Check(Native.GlobalMemoryStatusEx(ref memory), "Read current QA memory headroom");
        return memory;
    }
    private static void RequireMemoryHeadroom(Native.MemoryStatus memory, bool starting)
    {
        Require(HasMemoryHeadroom(memory.AvailablePhysical, memory.AvailablePageFile, starting),
            "QA_MEMORY_PRESSURE: " + (starting ? "Unity was not started" : "stopping only this owned isolated job")
            + "; availablePhysicalBytes=" + memory.AvailablePhysical
            + "; availableCommitBytes=" + memory.AvailablePageFile
            + "; requiredPhysicalBytes=" + (starting ? 2147483648UL : 1073741824UL)
            + "; requiredCommitBytes=2147483648; no other application or OS setting was changed.");
    }
    private static void Check(bool okay, string step) { if (!okay) throw new Win32Exception(Marshal.GetLastWin32Error(), step); }
    private static void Require(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct SecurityAttributes { internal int Length; internal IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] internal bool Inherit; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct StartupInfo
        {
            internal int Size; internal string Reserved, Desktop, Title;
            internal uint X, Y, XSize, YSize, XCount, YCount, Fill, Flags;
            internal ushort ShowWindow, ReservedSize; internal IntPtr ReservedBytes, Input, Output, Error;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
        [StructLayout(LayoutKind.Sequential)] internal struct UserFlags { internal int Inherit, Reserved; internal uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct UiRestrictions { internal uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct BasicLimit { internal long ProcessTime, JobTime; internal uint LimitFlags; internal UIntPtr MinimumWorkingSet, MaximumWorkingSet; internal uint ActiveProcessLimit; internal UIntPtr Affinity; internal uint PriorityClass, SchedulingClass; }
        [StructLayout(LayoutKind.Sequential)] internal struct IoCounters { internal ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] internal struct ExtendedLimit { internal BasicLimit Basic; internal IoCounters Io; internal UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
        [StructLayout(LayoutKind.Sequential)] internal struct MemoryStatus { internal uint Length, Load; internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateWindowStationW(string name, uint flags, uint access, ref SecurityAttributes security);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetProcessWindowStation(IntPtr station);
        [DllImport("user32.dll")] internal static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateDesktopExW(string name, string device, IntPtr mode, uint flags, uint access, ref SecurityAttributes security, uint heapKb, IntPtr reserved);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetThreadDesktop(IntPtr desktop);
        [DllImport("user32.dll")] internal static extern IntPtr GetThreadDesktop(uint threadId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool GetUserObjectInformationW(IntPtr handle, int index, StringBuilder buffer, uint length, out uint needed);
        [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)] internal static extern bool GetUserObjectFlags(IntPtr handle, int index, out UserFlags flags, uint length, out uint needed);
        [DllImport("user32.dll")] internal static extern bool CloseDesktop(IntPtr desktop);
        [DllImport("user32.dll")] internal static extern bool CloseWindowStation(IntPtr station);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool DestroyWindow(IntPtr window);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string descriptor, uint revision, out IntPtr result, out uint length);
        [DllImport("kernel32.dll")] internal static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll")] internal static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity, bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation process);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateJobObjectW(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool SetInformationJobObject(IntPtr job, int kind, IntPtr value, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool IsProcessInJob(IntPtr process, IntPtr job, out bool result);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint ResumeThread(IntPtr thread);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetExitCodeProcess(IntPtr process, out uint code);
        [DllImport("kernel32.dll")] internal static extern bool TerminateProcess(IntPtr process, uint code);
        [DllImport("kernel32.dll")] internal static extern bool TerminateJobObject(IntPtr job, uint code);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
    }
}
