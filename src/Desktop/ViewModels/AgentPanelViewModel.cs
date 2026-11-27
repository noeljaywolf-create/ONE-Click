using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using OneClick.Application.Agents;
using OneClick.Application.Agents.Takeoff;
using OneClick.Core.Ids;
using OneClick.Desktop.Services;

namespace OneClick.Desktop.ViewModels;

/// <summary>
/// ViewModel for the local deterministic agent execution panel.
/// The shell wires actual registry/runner execution through callbacks; this ViewModel does not
/// create services, registries, or network clients.
/// </summary>
public sealed class AgentPanelViewModel : ViewModelBase
{
    private AgentDescriptor? _selectedAgent;
    private string _projectIdInput = "proj-001";
    private string _fixturePathInput = string.Empty;
    private string _snapshotIdInput = "snap-001";
    private string _snapshotLabelInput = string.Empty;
    private bool _exportJson;
    private bool _exportCsv;
    private bool _isBusy;
    private bool _hasStatusError;
    private string _statusMessage = "Select a local deterministic agent and run it. No OpenAI/cloud LLM is used.";
    private string _lastRunAgentName = "(none)";
    private string _lastRunSuccess = "(not run)";
    private string _lastRunOutputSummary = "(none)";
    private string _lastRunError = "(none)";

    public AgentPanelViewModel()
    {
        RunAgentCommand = new AgentDelegateCommand(RunSelectedAgentAsync, () => !IsBusy && SelectedAgent is not null, OnCommandError);
        RefreshAgentsCommand = new AgentDelegateCommand(() => { RefreshAgents(); return Task.CompletedTask; }, () => !IsBusy, OnCommandError);

        Agents = new ObservableCollection<AgentDescriptor>();
        Issues = new ObservableCollection<string>();
        Warnings = new ObservableCollection<string>();
        ProvenanceRefs = new ObservableCollection<string>();
        RecentRunHistory = new ObservableCollection<AgentRunHistoryRow>();
    }

    /// <summary>Shell wiring: returns available local agents. Called by RefreshAgents/RefreshAgentsCommand.</summary>
    public Func<IReadOnlyList<AgentDescriptor>>? LoadAgentsAction { get; set; }

    /// <summary>
    /// Shell wiring: executes a local agent. The shell should dispatch through the local agent registry/runner
    /// and provide the appropriate <see cref="AgentRunContext"/>. Input is the typed first-slice request payload.
    /// </summary>
    public Func<AgentId, object?, CancellationToken, Task<AgentResult<object?>>>? ExecuteAgentAction { get; set; }

    /// <summary>Shell wiring: optional default project id provider used when refreshing/loading.</summary>
    public Func<string?>? GetDefaultProjectId { get; set; }

    /// <summary>Shell wiring: optional default fixture path provider for baseline load.</summary>
    public Func<string?>? GetDefaultFixturePath { get; set; }

    /// <summary>Shell wiring: returns recent local agent run records for audit/history display.</summary>
    public Func<IReadOnlyList<AgentRunRecord>>? LoadRecentRunsAction { get; set; }

    public ICommand RunAgentCommand { get; }
    public ICommand RefreshAgentsCommand { get; }

    public ObservableCollection<AgentDescriptor> Agents { get; }
    public ObservableCollection<string> Issues { get; }
    public ObservableCollection<string> Warnings { get; }
    public ObservableCollection<string> ProvenanceRefs { get; }
    public ObservableCollection<AgentRunHistoryRow> RecentRunHistory { get; }

    public AgentDescriptor? SelectedAgent
    {
        get => _selectedAgent;
        set
        {
            if (ReferenceEquals(_selectedAgent, value)) return;
            _selectedAgent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAgent));
            OnPropertyChanged(nameof(SelectedAgentName));
            OnPropertyChanged(nameof(SelectedAgentDescription));
            OnPropertyChanged(nameof(SelectedAgentVersion));
            OnPropertyChanged(nameof(SelectedAgentKind));
            OnPropertyChanged(nameof(IsBaselineLoadSelected));
            OnPropertyChanged(nameof(IsLedgerCalculationSelected));
            OnPropertyChanged(nameof(IsSnapshotFreezeSelected));
            OnPropertyChanged(nameof(ShowsFixturePath));
            OnPropertyChanged(nameof(ShowsSnapshotInputs));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasSelectedAgent => SelectedAgent is not null;
    public string SelectedAgentName => SelectedAgent?.Name ?? "(none selected)";
    public string SelectedAgentDescription => SelectedAgent?.Description ?? "Select an agent to see its description.";
    public string SelectedAgentVersion => SelectedAgent?.Version ?? "(n/a)";
    public string SelectedAgentKind => SelectedAgent is null ? "(n/a)" : SelectedAgent.Kind.ToString();

    public bool IsBaselineLoadSelected => IsSelected(BaselineLoadAgent.AgentDescriptor.Id);
    public bool IsLedgerCalculationSelected => IsSelected(LedgerCalculationAgent.AgentDescriptor.Id);
    public bool IsSnapshotFreezeSelected => IsSelected(SnapshotFreezeAgent.AgentDescriptor.Id);
    public bool ShowsFixturePath => IsBaselineLoadSelected;
    public bool ShowsSnapshotInputs => IsSnapshotFreezeSelected;

    public string ProjectIdInput
    {
        get => _projectIdInput;
        set { if (_projectIdInput == value) return; _projectIdInput = value; OnPropertyChanged(); }
    }

    public string FixturePathInput
    {
        get => _fixturePathInput;
        set { if (_fixturePathInput == value) return; _fixturePathInput = value; OnPropertyChanged(); }
    }

    public string SnapshotIdInput
    {
        get => _snapshotIdInput;
        set { if (_snapshotIdInput == value) return; _snapshotIdInput = value; OnPropertyChanged(); }
    }

    public string SnapshotLabelInput
    {
        get => _snapshotLabelInput;
        set { if (_snapshotLabelInput == value) return; _snapshotLabelInput = value; OnPropertyChanged(); }
    }

    public bool ExportJson
    {
        get => _exportJson;
        set { if (_exportJson == value) return; _exportJson = value; OnPropertyChanged(); }
    }

    public bool ExportCsv
    {
        get => _exportCsv;
        set { if (_exportCsv == value) return; _exportCsv = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasStatusError
    {
        get => _hasStatusError;
        private set { if (_hasStatusError == value) return; _hasStatusError = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
    }

    public string LastRunAgentName
    {
        get => _lastRunAgentName;
        private set { if (_lastRunAgentName == value) return; _lastRunAgentName = value; OnPropertyChanged(); }
    }

    public string LastRunSuccess
    {
        get => _lastRunSuccess;
        private set { if (_lastRunSuccess == value) return; _lastRunSuccess = value; OnPropertyChanged(); }
    }

    public string LastRunOutputSummary
    {
        get => _lastRunOutputSummary;
        private set { if (_lastRunOutputSummary == value) return; _lastRunOutputSummary = value; OnPropertyChanged(); }
    }

    public string LastRunError
    {
        get => _lastRunError;
        private set { if (_lastRunError == value) return; _lastRunError = value; OnPropertyChanged(); }
    }

    public void RefreshAgents()
    {
        var selectedId = SelectedAgent?.Id;
        Agents.Clear();

        IReadOnlyList<AgentDescriptor> descriptors;
        try
        {
            descriptors = LoadAgentsAction?.Invoke() ?? Array.Empty<AgentDescriptor>();
        }
        catch (Exception ex)
        {
            SetStatus("Failed to load agents: " + ex.Message, isError: true);
            return;
        }

        foreach (var descriptor in descriptors.OrderBy(d => d.Name, StringComparer.Ordinal))
            Agents.Add(descriptor);

        if (Agents.Count == 0)
        {
            SelectedAgent = null;
            SetStatus("No local agents available. The shell must wire LoadAgentsAction to a local agent registry.", isError: true);
            return;
        }

        SelectedAgent = selectedId is not null
            ? Agents.FirstOrDefault(a => a.Id.Equals(selectedId)) ?? Agents[0]
            : Agents[0];

        ApplyDefaultsFromShell();
        var historyError = RefreshRunHistory();
        var status = $"Loaded {Agents.Count} local deterministic agent(s). No OpenAI/cloud LLM is used.";
        if (historyError is not null)
            status += " Run history refresh failed: " + historyError;
        SetStatus(status, isError: historyError is not null);
    }

    /// <summary>
    /// Refreshes recent local run history from the shell-wired callback.
    /// Returns null on success (or when no callback is wired); otherwise returns an error message.
    /// </summary>
    public string? RefreshRunHistory()
    {
        RecentRunHistory.Clear();

        IReadOnlyList<AgentRunRecord> records;
        try
        {
            records = LoadRecentRunsAction?.Invoke() ?? Array.Empty<AgentRunRecord>();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        foreach (var record in records)
            RecentRunHistory.Add(AgentRunHistoryRow.FromRecord(record));

        return null;
    }

    private async Task RunSelectedAgentAsync()
    {
        if (IsBusy) return;
        if (SelectedAgent is null)
        {
            SetStatus("Select an agent before running.", isError: true);
            return;
        }

        if (ExecuteAgentAction is null)
        {
            SetStatus("Agent execution is not wired. The shell must provide ExecuteAgentAction.", isError: true);
            return;
        }

        if (!TryBuildInput(SelectedAgent.Id, out var input, out var inputError))
        {
            SetStatus(inputError, isError: true);
            return;
        }

        try
        {
            IsBusy = true;
            ClearRunDetails();
            LastRunAgentName = $"{SelectedAgent.Name} ({SelectedAgent.Id.Value})";
            SetStatus($"Running {SelectedAgent.Name}...");

            var result = await ExecuteAgentAction(SelectedAgent.Id, input, CancellationToken.None).ConfigureAwait(true);
            ApplyResult(result);
            var historyError = RefreshRunHistory();
            if (historyError is not null)
                SetStatus(StatusMessage + " Run history refresh failed: " + historyError, isError: true);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Agent run canceled.", isError: true);
            LastRunSuccess = "Canceled";
        }
        catch (Exception ex)
        {
            SetStatus("Agent run failed: " + ex.Message, isError: true);
            LastRunSuccess = "Failed";
            LastRunError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildInput(AgentId agentId, out object? input, out string error)
    {
        input = null;
        error = string.Empty;

        var projectIdText = (ProjectIdInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(projectIdText))
        {
            error = "Project ID is required.";
            return false;
        }

        var projectId = new ProjectId(projectIdText);

        if (agentId.Equals(BaselineLoadAgent.AgentDescriptor.Id))
        {
            var fixturePath = (FixturePathInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(fixturePath))
            {
                error = "Fixture path is required for Baseline Load.";
                return false;
            }

            input = new BaselineLoadInput(projectId, fixturePath);
            return true;
        }

        if (agentId.Equals(LedgerCalculationAgent.AgentDescriptor.Id))
        {
            input = new LedgerCalculationInput(projectId);
            return true;
        }

        if (agentId.Equals(SnapshotFreezeAgent.AgentDescriptor.Id))
        {
            var snapshotId = (SnapshotIdInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                error = "Snapshot ID is required for Snapshot Freeze.";
                return false;
            }

            var label = string.IsNullOrWhiteSpace(SnapshotLabelInput) ? null : SnapshotLabelInput.Trim();
            input = new SnapshotFreezeInput(
                projectId,
                snapshotId,
                DateTimeOffset.UtcNow,
                label,
                Exclusions: null,
                ExportJson: ExportJson,
                ExportCsv: ExportCsv);
            return true;
        }

        error = $"No simple input mapping is available for agent '{agentId.Value}'. The shell can extend the panel for additional local agents.";
        return false;
    }

    private void ApplyResult(AgentResult<object?> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var issue in result.Issues) Issues.Add(issue);
        foreach (var warning in result.Warnings) Warnings.Add(warning);
        foreach (var provenance in result.ProvenanceRefs) ProvenanceRefs.Add(provenance);

        LastRunSuccess = result.IsSuccess ? "Success" : "Failure";
        LastRunOutputSummary = BuildOutputSummary(result.Output);
        LastRunError = result.IsSuccess
            ? "(none)"
            : $"{result.ErrorCode ?? "error"}: {result.ErrorDetail ?? "Unknown error"}";

        if (result.IsSuccess)
        {
            SetStatus($"Agent '{SelectedAgent?.Name}' completed successfully. Review issues/warnings/provenance before using outputs.");
        }
        else
        {
            SetStatus($"Agent '{SelectedAgent?.Name}' failed: {LastRunError}", isError: true);
        }
    }

    private void ClearRunDetails()
    {
        Issues.Clear();
        Warnings.Clear();
        ProvenanceRefs.Clear();
        LastRunSuccess = "(running)";
        LastRunOutputSummary = "(none)";
        LastRunError = "(none)";
    }

    private static string BuildOutputSummary(object? output)
    {
        if (output is null) return "(no output payload)";

        switch (output)
        {
            case BaselineLoadOutput baseline:
                return $"Project={baseline.ProjectId.Value}; Revision={baseline.Revision}; Pieces={baseline.PieceCount}; Fixture={baseline.FixturePath}; SourceRefs={baseline.SourceRefs.Count}";
            case LedgerCalculationOutput ledger:
                return $"Project={ledger.ProjectId.Value}; Revision={ledger.ProjectRevision}; Pieces={ledger.PieceCount}; RoutePieces={ledger.Ledgers.Pieces.Count}; IssueIds={ledger.Ledgers.IssueIds.Count}";
            case SnapshotFreezeOutput snapshot:
                return $"Project={snapshot.ProjectId.Value}; Snapshot={snapshot.SnapshotId}; Revision={snapshot.ProjectRevision}; Known={snapshot.Manifest.KnownQuantities.Count}; Provisional={snapshot.Manifest.ProvisionalQuantities.Count}; Unknown={snapshot.Manifest.UnknownQuantities.Count}; JSON={(snapshot.JsonExport is null ? "no" : "yes")}; CSV={(snapshot.CsvExport is null ? "no" : "yes")}";
            default:
                return output.ToString() ?? output.GetType().FullName ?? "(output)";
        }
    }

    private void ApplyDefaultsFromShell()
    {
        var defaultProjectId = GetDefaultProjectId?.Invoke();
        if (!string.IsNullOrWhiteSpace(defaultProjectId))
            ProjectIdInput = defaultProjectId.Trim();

        var defaultFixturePath = GetDefaultFixturePath?.Invoke();
        if (!string.IsNullOrWhiteSpace(defaultFixturePath))
            FixturePathInput = defaultFixturePath.Trim();
    }

    private bool IsSelected(AgentId id) => SelectedAgent is not null && SelectedAgent.Id.Equals(id);

    private void SetStatus(string message, bool isError = false)
    {
        HasStatusError = isError;
        StatusMessage = message;
    }

    private void OnCommandError(Exception ex)
    {
        SetStatus("Command failed: " + ex.Message, isError: true);
        IsBusy = false;
    }
}
