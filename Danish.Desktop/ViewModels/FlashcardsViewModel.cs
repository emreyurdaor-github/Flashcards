using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Danish.Desktop.Data;
using Danish.Desktop.Models;
using Danish.Desktop.Services;

namespace Danish.Desktop.ViewModels;

public class FlashcardsViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RotationInterval = TimeSpan.FromSeconds(20);

    private readonly IReadOnlyList<FlashcardEntry> _flashcards = FlashcardDataSource.GetFlashcards();
    private readonly DispatcherTimer _rotationTimer;
    private readonly Random _random = new();
    private readonly AudioService _audioService = new();
    private readonly Stack<FlashcardEntry> _navHistory = new();
    private readonly HashSet<FlashcardEntry> _viewedInSession = new();

    private FlashcardEntry? _currentFlashcard;
    private bool _isAddRecordPage;
    private bool _isRotationPaused = true;
    private bool _isMuted = true;
    private bool _isAudioPlaying;
    private string _selectedTypeFilter = "All";
    private string _searchText = string.Empty;
    private string _validationMessage = string.Empty;
    private bool _isEditMode;

    // Add/Edit form fields
    private string _newDanish = string.Empty;
    private string _newEnglish = string.Empty;
    private string _newType = string.Empty;
    private string _newConjugation = string.Empty;
    private string _newExampleDanish = string.Empty;
    private string _newExampleEnglish = string.Empty;
    private string _newContextualTip = string.Empty;
    private string _newMnemonic = string.Empty;

    private FormattedExampleText _highlightedExampleDanish  = new();
    private FormattedExampleText _highlightedExampleEnglish = new();
    private ObservableCollection<FlashcardEntry> _searchResults = new();

    // ─── Commands ────────────────────────────────────────────────────────────────
    public IRelayCommand PreviousCardCommand       { get; }
    public IRelayCommand NextCardCommand           { get; }
    public IRelayCommand PlayCommand               { get; }
    public IRelayCommand PauseCommand              { get; }
    public IRelayCommand ToggleMuteCommand         { get; }
    public IRelayCommand ShowAddRecordCommand      { get; }
    public IRelayCommand ShowEditRecordCommand     { get; }
    public IRelayCommand SaveNewRecordCommand      { get; }
    public IRelayCommand CancelAddRecordCommand    { get; }
    public IAsyncRelayCommand PlayDanishWordCommand   { get; }
    public IAsyncRelayCommand PlayDanishExampleCommand { get; }
    public IRelayCommand<FlashcardEntry?> SelectSearchResultCommand { get; }

    // ─── Properties ─────────────────────────────────────────────────────────────

    public IReadOnlyList<string> TypeFilters { get; } = new[] { "All", "v.", "adj.", "n.", "adv.", "prep.", "conj." };

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                _navHistory.Clear();
                _viewedInSession.Clear();
                SelectNextFlashcard();
            }
        }
    }

    public FlashcardEntry? CurrentFlashcard
    {
        get => _currentFlashcard;
        private set
        {
            if (SetProperty(ref _currentFlashcard, value))
            {
                OnPropertyChanged(nameof(CurrentDanish));
                OnPropertyChanged(nameof(CurrentEnglish));
                OnPropertyChanged(nameof(CurrentTypes));
                OnPropertyChanged(nameof(HasCurrentType));
                OnPropertyChanged(nameof(CurrentConjugation));
                OnPropertyChanged(nameof(HasCurrentConjugation));
                OnPropertyChanged(nameof(CurrentExampleDanish));
                OnPropertyChanged(nameof(HasCurrentExampleDanish));
                OnPropertyChanged(nameof(CurrentExampleEnglish));
                OnPropertyChanged(nameof(HasCurrentExampleEnglish));
                OnPropertyChanged(nameof(CurrentContextualTip));
                OnPropertyChanged(nameof(HasCurrentContextualTip));
                OnPropertyChanged(nameof(CurrentMnemonic));
                OnPropertyChanged(nameof(HasCurrentMnemonic));
                OnPropertyChanged(nameof(HasFlashcards));
                UpdateHighlightedExamples();
                OnPropertyChanged(nameof(HighlightedExampleDanish));
                OnPropertyChanged(nameof(HighlightedExampleEnglish));
                if (value is not null) _viewedInSession.Add(value);
                OnPropertyChanged(nameof(FlashcardProgressText));
                _ = PlayDanishWordTwiceAsync();
            }
        }
    }

    public string CurrentDanish     => _currentFlashcard?.Danish     ?? "No flashcards available";
    public string CurrentEnglish    => _currentFlashcard?.English    ?? string.Empty;
    public string? CurrentConjugation => _currentFlashcard?.Conjugation;
    public bool HasCurrentConjugation => _currentFlashcard?.HasConjugation == true;

    public IReadOnlyList<string> CurrentTypes =>
        string.IsNullOrWhiteSpace(_currentFlashcard?.Type) ? [] :
        _currentFlashcard.Type.Split('/').Select(t => t.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

    public bool HasCurrentType => !string.IsNullOrWhiteSpace(_currentFlashcard?.Type);

    public string? CurrentExampleDanish   => _currentFlashcard?.ExampleDanish;
    public bool HasCurrentExampleDanish   => _currentFlashcard?.HasExampleDanish == true;
    public string? CurrentExampleEnglish  => _currentFlashcard?.ExampleEnglish;
    public bool HasCurrentExampleEnglish  => _currentFlashcard?.HasExampleEnglish == true;
    public string? CurrentContextualTip   => _currentFlashcard?.ContextualTip;
    public bool HasCurrentContextualTip   => _currentFlashcard?.HasContextualTip == true;
    public string? CurrentMnemonic        => _currentFlashcard?.Mnemonic;
    public bool HasCurrentMnemonic        => _currentFlashcard?.HasMnemonic == true;
    public bool HasFlashcards             => _currentFlashcard is not null;

    public string FlashcardProgressText => $"{_viewedInSession.Count} of {GetFiltered().Count}";

    public FormattedExampleText HighlightedExampleDanish
    {
        get => _highlightedExampleDanish;
        private set => SetProperty(ref _highlightedExampleDanish, value);
    }

    public FormattedExampleText HighlightedExampleEnglish
    {
        get => _highlightedExampleEnglish;
        private set => SetProperty(ref _highlightedExampleEnglish, value);
    }

    public bool IsAddRecordPage
    {
        get => _isAddRecordPage;
        private set
        {
            if (SetProperty(ref _isAddRecordPage, value))
            {
                OnPropertyChanged(nameof(IsFlashcardPage));
                UpdateRotationState();
            }
        }
    }
    public bool IsFlashcardPage => !IsAddRecordPage;

    public bool IsRotationPaused
    {
        get => _isRotationPaused;
        private set
        {
            if (SetProperty(ref _isRotationPaused, value))
            {
                OnPropertyChanged(nameof(IsRotationPlaying));
                UpdateRotationState();
            }
        }
    }
    public bool IsRotationPlaying => !IsRotationPaused;

    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        private set
        {
            if (SetProperty(ref _isAudioPlaying, value))
                OnPropertyChanged(nameof(CanNavigateFlashcards));
        }
    }
    public bool CanNavigateFlashcards => !_isAudioPlaying;

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteTooltip));
            }
        }
    }
    public string MuteTooltip => IsMuted ? "Unmute translations" : "Mute translations";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != (value ?? string.Empty))
            {
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                UpdateSearchResults();
            }
        }
    }

    public ObservableCollection<FlashcardEntry> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set { if (SetProperty(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); }
    }
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    // Form fields
    public string NewDanish       { get => _newDanish;       set => SetProperty(ref _newDanish, value); }
    public string NewEnglish      { get => _newEnglish;      set => SetProperty(ref _newEnglish, value); }
    public string NewType         { get => _newType;         set => SetProperty(ref _newType, value); }
    public string NewConjugation  { get => _newConjugation;  set => SetProperty(ref _newConjugation, value); }
    public string NewExampleDanish  { get => _newExampleDanish;  set => SetProperty(ref _newExampleDanish, value); }
    public string NewExampleEnglish { get => _newExampleEnglish; set => SetProperty(ref _newExampleEnglish, value); }
    public string NewContextualTip  { get => _newContextualTip;  set => SetProperty(ref _newContextualTip, value); }
    public string NewMnemonic       { get => _newMnemonic;       set => SetProperty(ref _newMnemonic, value); }

    // ─── Constructor ─────────────────────────────────────────────────────────────

    public FlashcardsViewModel()
    {
        PreviousCardCommand    = new RelayCommand(SelectPreviousFlashcard);
        NextCardCommand        = new RelayCommand(SelectNextFlashcard);
        PlayCommand            = new RelayCommand(() => IsRotationPaused = false);
        PauseCommand           = new RelayCommand(() => IsRotationPaused = true);
        ToggleMuteCommand      = new RelayCommand(() => IsMuted = !IsMuted);
        ShowAddRecordCommand   = new RelayCommand(ShowAddRecord);
        ShowEditRecordCommand  = new RelayCommand(ShowEditRecord);
        SaveNewRecordCommand   = new RelayCommand(SaveNewRecord);
        CancelAddRecordCommand = new RelayCommand(CancelAddRecord);
        PlayDanishWordCommand    = new AsyncRelayCommand(PlayCurrentDanishWord);
        PlayDanishExampleCommand = new AsyncRelayCommand(PlayCurrentDanishExample);
        SelectSearchResultCommand = new RelayCommand<FlashcardEntry?>(SelectSearchResult);

        _rotationTimer = new DispatcherTimer { Interval = RotationInterval };
        _rotationTimer.Tick += (_, _) => SelectNextFlashcard();

        SelectNextFlashcard();
        UpdateRotationState();
    }

    // ─── Navigation ──────────────────────────────────────────────────────────────

    private IReadOnlyList<FlashcardEntry> GetFiltered()
    {
        if (string.IsNullOrEmpty(_selectedTypeFilter) || _selectedTypeFilter == "All") return _flashcards;
        return _flashcards.Where(f => !string.IsNullOrWhiteSpace(f.Type) &&
            f.Type.Split('/').Select(t => t.Trim()).Any(t => t == _selectedTypeFilter)).ToList();
    }

    private void SelectNextFlashcard()
    {
        var pool = GetFiltered();
        if (pool.Count == 0) { CurrentFlashcard = null; return; }
        if (_currentFlashcard is not null) _navHistory.Push(_currentFlashcard);
        if (pool.Count == 1) { CurrentFlashcard = pool[0]; return; }
        FlashcardEntry next;
        do { next = pool[_random.Next(pool.Count)]; } while (ReferenceEquals(next, _currentFlashcard));
        CurrentFlashcard = next;
    }

    private void SelectPreviousFlashcard()
    {
        if (_navHistory.Count == 0) return;
        var prev = _navHistory.Pop();
        if (ReferenceEquals(prev, _currentFlashcard)) { SelectPreviousFlashcard(); return; }
        CurrentFlashcard = prev;
    }

    private void UpdateRotationState()
    {
        if (IsAddRecordPage || _flashcards.Count <= 1 || IsRotationPaused) { _rotationTimer.Stop(); return; }
        if (!_rotationTimer.IsEnabled) _rotationTimer.Start();
    }

    // ─── Audio ───────────────────────────────────────────────────────────────────

    private async Task PlayCurrentDanishWord()
    {
        if (_currentFlashcard is null) return;
        try { IsAudioPlaying = true; await _audioService.PlayDanishPronunciation(_currentFlashcard.Danish); }
        finally { IsAudioPlaying = false; }
    }

    private async Task PlayCurrentDanishExample()
    {
        if (_currentFlashcard is null || string.IsNullOrWhiteSpace(_currentFlashcard.ExampleDanish)) return;
        try { IsAudioPlaying = true; await _audioService.PlayDanishPronunciation(_currentFlashcard.ExampleDanish); }
        finally { IsAudioPlaying = false; }
    }

    private async Task PlayDanishWordTwiceAsync()
    {
        if (IsMuted || _currentFlashcard is null) return;
        try
        {
            IsAudioPlaying = true;
            await _audioService.PlayDanishPronunciation(_currentFlashcard.Danish);
            await Task.Delay(500);
            await _audioService.PlayDanishPronunciation(_currentFlashcard.Danish);
            await Task.Delay(1000);
            if (!string.IsNullOrWhiteSpace(_currentFlashcard.English))
                await _audioService.PlayEnglishPronunciation(_currentFlashcard.English);
            await Task.Delay(500);
            if (!string.IsNullOrWhiteSpace(_currentFlashcard.ExampleDanish))
                await _audioService.PlayDanishPronunciation(_currentFlashcard.ExampleDanish);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VM] Audio error: {ex.Message}"); }
        finally { IsAudioPlaying = false; }
    }

    // ─── Search ──────────────────────────────────────────────────────────────────

    private void UpdateSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        var lower = SearchText.Trim().ToLowerInvariant();
        foreach (var r in _flashcards.Where(f => f.Danish.ToLowerInvariant().Contains(lower)).OrderBy(f => f.Danish))
            SearchResults.Add(r);
    }

    private void SelectSearchResult(FlashcardEntry? fc)
    {
        if (fc is null) return;
        CurrentFlashcard = fc;
        Dispatcher.UIThread.InvokeAsync(() => SearchText = string.Empty, DispatcherPriority.Input);
    }

    // ─── Add / Edit ──────────────────────────────────────────────────────────────

    private void ShowAddRecord()  { ResetForm(); IsAddRecordPage = true; _isEditMode = false; }

    private void ShowEditRecord()
    {
        if (_currentFlashcard is null) return;
        NewDanish       = _currentFlashcard.Danish;
        NewEnglish      = _currentFlashcard.English;
        NewType         = _currentFlashcard.Type ?? string.Empty;
        NewConjugation  = _currentFlashcard.Conjugation  ?? string.Empty;
        NewExampleDanish  = _currentFlashcard.ExampleDanish  ?? string.Empty;
        NewExampleEnglish = _currentFlashcard.ExampleEnglish ?? string.Empty;
        NewContextualTip  = _currentFlashcard.ContextualTip  ?? string.Empty;
        NewMnemonic       = _currentFlashcard.Mnemonic       ?? string.Empty;
        _isEditMode = true;
        IsAddRecordPage = true;
    }

    private void SaveNewRecord()
    {
        var danish  = NewDanish.Trim();
        var english = NewEnglish.Trim();
        var type    = NewType.Trim();
        if (string.IsNullOrWhiteSpace(danish) || string.IsNullOrWhiteSpace(english)) { ValidationMessage = "Danish and English are required."; return; }
        if (string.IsNullOrWhiteSpace(type)) { ValidationMessage = "Type is required (e.g. v., n., adj.)."; return; }

        var fc = new FlashcardEntry
        {
            Danish        = danish,
            English       = english,
            Type          = type,
            Conjugation   = NullIfEmpty(NewConjugation),
            ExampleDanish = NullIfEmpty(NewExampleDanish),
            ExampleEnglish = NullIfEmpty(NewExampleEnglish),
            ContextualTip = NullIfEmpty(NewContextualTip),
            Mnemonic      = NullIfEmpty(NewMnemonic),
        };

        if (_isEditMode)
        {
            var orig = _currentFlashcard?.Danish ?? string.Empty;
            if (!FlashcardDataSource.TryUpdateFlashcard(orig, fc)) { ValidationMessage = "Unable to update: conflicts with an existing record."; return; }
            CurrentFlashcard = fc;
            _isEditMode = false;
        }
        else
        {
            if (!FlashcardDataSource.TryAddFlashcard(fc)) { ValidationMessage = $"A record with Danish '{danish}' already exists."; return; }
            CurrentFlashcard = fc;
        }

        ResetForm();
        IsAddRecordPage = false;
    }

    private void CancelAddRecord() { ResetForm(); IsAddRecordPage = false; _isEditMode = false; }

    private void ResetForm()
    {
        NewDanish = NewEnglish = NewType = NewConjugation = string.Empty;
        NewExampleDanish = NewExampleEnglish = NewContextualTip = NewMnemonic = string.Empty;
        ValidationMessage = string.Empty;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ─── Highlighted examples ────────────────────────────────────────────────────

    private void UpdateHighlightedExamples()
    {
        HighlightedExampleDanish  = string.IsNullOrEmpty(CurrentExampleDanish) || string.IsNullOrEmpty(CurrentDanish)
            ? new FormattedExampleText { FullText = CurrentExampleDanish ?? string.Empty }
            : HighlightWord(CurrentExampleDanish!, CurrentDanish) is var dr && string.IsNullOrEmpty(dr.HighlightedWord) && !string.IsNullOrEmpty(CurrentConjugation)
                ? HighlightWord(CurrentExampleDanish!, CurrentConjugation!) : dr;

        HighlightedExampleEnglish = string.IsNullOrEmpty(CurrentExampleEnglish) || string.IsNullOrEmpty(CurrentEnglish)
            ? new FormattedExampleText { FullText = CurrentExampleEnglish ?? string.Empty }
            : HighlightWord(CurrentExampleEnglish!, CurrentEnglish);
    }

    private FormattedExampleText HighlightWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            return new FormattedExampleText { FullText = text };

        foreach (var phrase in ExtractPhrases(word))
        {
            int idx = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return Extract(text, idx, phrase.Length);
        }
        foreach (var kw in ExtractKeywords(word))
        {
            var (idx, len) = FindWholeWord(text.ToLowerInvariant(), kw);
            if (idx >= 0) return Extract(text, idx, len);
        }
        return new FormattedExampleText { FullText = text };
    }

    private static List<string> ExtractPhrases(string word)
    {
        var skip = new[] { "to", "at", "de", "for", "en", "et" };
        var list = new List<string>();
        foreach (var part in word.Split(new[] { '/', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var words = part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int start = words.Length > 0 && Array.Exists(skip, p => p.Equals(words[0], StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            if (words.Length - start > 1)
            {
                var phrase = string.Join(" ", words.Skip(start));
                if (!list.Contains(phrase, StringComparer.OrdinalIgnoreCase)) list.Add(phrase);
            }
        }
        return list;
    }

    private static List<string> ExtractKeywords(string word)
    {
        var skip = new[] { "to", "at", "de", "for", "en", "et" };
        var list = new List<string>();
        foreach (var part in word.Split(new[] { '/', '|' }, StringSplitOptions.RemoveEmptyEntries))
            foreach (var w in part.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = w.ToLowerInvariant();
                if (l.Length <= 1 || Array.Exists(skip, p => p == l)) continue;
                if (!list.Contains(l, StringComparer.OrdinalIgnoreCase)) list.Add(l);
            }
        return list;
    }

    private static (int idx, int len) FindWholeWord(string text, string word)
    {
        int i = 0;
        while ((i = text.IndexOf(word, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            bool startOk = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
            bool endOk   = i + word.Length >= text.Length || !char.IsLetterOrDigit(text[i + word.Length]);
            if (startOk && endOk) return (i, word.Length);
            i++;
        }
        return (-1, 0);
    }

    private static FormattedExampleText Extract(string text, int idx, int len) => new()
    {
        BeforeWord      = text[..idx],
        HighlightedWord = text.Substring(idx, len),
        AfterWord       = text[(idx + len)..],
        FullText        = text,
    };

    // ─── Dispose ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _rotationTimer.Stop();
        _audioService.Dispose();
    }
}
