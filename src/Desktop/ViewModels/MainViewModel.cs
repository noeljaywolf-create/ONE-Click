using OneClick.Application.Contracts;
using OneClick.Application.Persistence;
using OneClick.Core.Domain;
using OneClick.Core.Ids;
using OneClick.Desktop.Services;

namespace OneClick.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly DesktopProjectBootstrapper _bootstrapper;
    private string _projectId = string.Empty;
    private long _projectRevision;
    private string _statusMessage = "Not initialized.";
    private bool _isLoaded;
    private bool _isBusy;
    private int _pieceCount;
    private int _snapshotManifestCount;
    private string _knownLimitations = string.Empty;
    private string _lastRefreshedDisplay = "never";

    public MainViewModel()
        : this(new DesktopProjectBootstrapper())
    {
    }

    public MainViewModel(DesktopProjectBootstrapper bootstrapper)
    {
        _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        InitializeCommand = new SnapshotDelegateCommand(InitializeAsync, () => !IsBusy);
    }

    public SnapshotDelegateCommand InitializeCommand { get; }

    public string ProjectId
    {
        get => _projectId;
        private set => SetProperty(ref _projectId, value);
    }

    public long ProjectRevision
    {
        get => _projectRevision;
        internal set => SetProperty(ref _projectRevision, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        internal set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetProperty(ref _isLoaded, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                InitializeCommand.RaiseCanExecuteChanged();
        }
    }

    public int PieceCount
    {
        get => _pieceCount;
        private set => SetProperty(ref _pieceCount, value);
    }

    public int SnapshotManifestCount
    {
        get => _snapshotManifestCount;
        internal set => SetProperty(ref _snapshotManifestCount, value);
    }

    public void SyncFromDomainState(ProjectDomainState state, string? statusMessage = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        ProjectRevision = state.Revision;
        PieceCount = state.Pieces.Count;
        SnapshotManifestCount = state.SnapshotManifests.Count;
        LastRefreshedDisplay = DateTimeOffset.Now.ToString("HH:mm:ss");

        if (!string.IsNullOrWhiteSpace(statusMessage))
            StatusMessage = statusMessage;
    }

    public string KnownLimitations
    {
        get => _knownLimitations;
        private set => SetProperty(ref _knownLimitations, value);
    }

    public string LastRefreshedDisplay
    {
        get => _lastRefreshedDisplay;
        internal set => SetProperty(ref _lastRefreshedDisplay, value);
    }

    public IProjectCommandService? CommandService { get; private set; }
    public IProjectQueryService? QueryService { get; private set; }
    public IProjectDomainStateStore? DomainStateStore { get; private set; }
    public ActorContext? ActorContext { get; private set; }
    public ProjectId? CurrentProjectId { get; private set; }

    /// <summary>Optional shell callback invoked after a successful baseline load/reload.</summary>
    public Func<Task>? AfterInitializeAsync { get; set; }

    public async Task InitializeAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "Loading synthetic reference baseline...";
        try
        {
            var result = await _bootstrapper.LoadSyntheticBaselineAsync().ConfigureAwait(true);

            CommandService = result.CommandService;
            QueryService = result.QueryService;
            DomainStateStore = result.DomainStateStore;
            ActorContext = result.ActorContext;
            CurrentProjectId = result.ProjectId;

            ProjectId = result.ProjectId.Value;
            ProjectRevision = result.Revision.Value;
            PieceCount = result.PieceCount;
            SnapshotManifestCount = result.SnapshotManifestCount;
            KnownLimitations = "in-memory-skeleton; synthetic baseline only";
            IsLoaded = true;
            StatusMessage = result.StatusMessage;
            if (AfterInitializeAsync is not null)
                await AfterInitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            IsLoaded = false;
            StatusMessage = $"Initialization failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
