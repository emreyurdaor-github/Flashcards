using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Flashcards.Data;
using Flashcards.Models;
using Flashcards.Services;

namespace Flashcards.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan RotationInterval = TimeSpan.FromSeconds(20);
    private readonly IReadOnlyList<FlashcardEntry> _flashcards = FlashcardDataSource.GetFlashcards();
    private readonly IReadOnlyList<WritingEntry> _writingEntries = WritingDataSource.GetWritingEntries();
    private readonly DispatcherTimer _rotationTimer;
    private readonly Random _random = new();
    private readonly AudioService _audioService = new();
    private readonly Stack<FlashcardEntry> _navigationHistory = new();
    private FlashcardEntry? _currentFlashcard;
    private WritingEntry? _currentWritingEntry;
    private bool _isAddRecordPage;
    private bool _isAddWritingPage;
    private bool _isRotationPaused = true;
    private bool _isMuted = true;
    private int _selectedTabIndex = 0;
    private string _newDanish = string.Empty;
    private string _newEnglish = string.Empty;
    private string _newType = string.Empty;
    private string _newConjugation = string.Empty;
    private string _newExampleDanish = string.Empty;
    private string _newExampleEnglish = string.Empty;
    private string _newContextualTip = string.Empty;
    private string _newWritingDanish = string.Empty;
    private string _newWritingEnglish = string.Empty;
    private string _newWritingDanishTitle = string.Empty;
    private string _newWritingEnglishTitle = string.Empty;
    private string _validationMessage = string.Empty;
    private string _searchText = string.Empty;
    private bool _isAudioPlaying = false;
    
    // Search autocomplete collection
    private ObservableCollection<FlashcardEntry> _searchResults = new();

    public string DataSourceName { get; } = "Local in-solution data source";
    public IRelayCommand ShowAddRecordCommand { get; }
    public IRelayCommand SaveNewRecordCommand { get; }
    public IRelayCommand CancelAddRecordCommand { get; }
    public IRelayCommand ShowEditRecordCommand { get; }
    public IRelayCommand ShowAddWritingCommand { get; }
    public IRelayCommand SaveNewWritingCommand { get; }
    public IRelayCommand CancelAddWritingCommand { get; }
    public IRelayCommand ShowEditWritingCommand { get; }
    public IRelayCommand NextWritingCommand { get; }
    public IRelayCommand PreviousWritingCommand { get; }
    public IRelayCommand PlayCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand PreviousCardCommand { get; }
    public IRelayCommand NextCardCommand { get; }
    public IAsyncRelayCommand PlayDanishWordCommand { get; }
    public IAsyncRelayCommand PlayDanishExampleCommand { get; }
    public IRelayCommand MinimizeToTrayCommand { get; set; }
    public IRelayCommand<FlashcardEntry?> SelectSearchResultCommand { get; }
    public IRelayCommand ToggleMuteCommand { get; }

    public FlashcardEntry? CurrentFlashcard
    {
        get => _currentFlashcard;
        private set
        {
            if (SetProperty(ref _currentFlashcard, value))
            {
                OnPropertyChanged(nameof(CurrentDanish));
                OnPropertyChanged(nameof(CurrentEnglish));
                OnPropertyChanged(nameof(CurrentType));
                OnPropertyChanged(nameof(HasCurrentType));
                OnPropertyChanged(nameof(CurrentTypes));
                OnPropertyChanged(nameof(CurrentConjugation));
                OnPropertyChanged(nameof(HasCurrentConjugation));
                OnPropertyChanged(nameof(CurrentExampleDanish));
                OnPropertyChanged(nameof(HasCurrentExampleDanish));
                OnPropertyChanged(nameof(CurrentExampleEnglish));
                OnPropertyChanged(nameof(HasCurrentExampleEnglish));
                OnPropertyChanged(nameof(CurrentContextualTip));
                OnPropertyChanged(nameof(HasCurrentContextualTip));
                OnPropertyChanged(nameof(HasFlashcards));
                
                // Update highlighted examples
                UpdateHighlightedExamples();
                OnPropertyChanged(nameof(HighlightedExampleDanish));
                OnPropertyChanged(nameof(HighlightedExampleEnglish));
                
                // Play the Danish example sound twice with 1 second delay
                _ = PlayDanishWordTwiceAsync();
            }
        }
    }

    public string CurrentDanish => CurrentFlashcard?.Danish ?? "No flashcards available";

    public string CurrentEnglish => CurrentFlashcard?.English ?? string.Empty;

    public string? CurrentType => CurrentFlashcard?.Type;

    public bool HasCurrentType => !string.IsNullOrWhiteSpace(CurrentFlashcard?.Type);

    public IReadOnlyList<string> CurrentTypes =>
        string.IsNullOrWhiteSpace(CurrentFlashcard?.Type)
            ? []
            : CurrentFlashcard.Type
                .Split('/')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

    public string? CurrentConjugation => CurrentFlashcard?.Conjugation;

    public bool HasCurrentConjugation => CurrentFlashcard?.HasConjugation == true;

    public string? CurrentExampleDanish => CurrentFlashcard?.ExampleDanish;

    public bool HasCurrentExampleDanish => CurrentFlashcard?.HasExampleDanish == true;

    public string? CurrentExampleEnglish => CurrentFlashcard?.ExampleEnglish;

    public bool HasCurrentExampleEnglish => CurrentFlashcard?.HasExampleEnglish == true;

    public string? CurrentContextualTip => CurrentFlashcard?.ContextualTip;

    public bool HasCurrentContextualTip => CurrentFlashcard?.HasContextualTip == true;

    public string? CurrentExampleDanishHighlighted
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentExampleDanish) || string.IsNullOrEmpty(CurrentDanish))
                return CurrentExampleDanish;
            
            // Return the example as-is; highlighting will be done via visual styling
            return CurrentExampleDanish;
        }
    }

    public string? CurrentExampleEnglishHighlighted
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentExampleEnglish) || string.IsNullOrEmpty(CurrentEnglish))
                return CurrentExampleEnglish;
            
            // Return the example as-is; highlighting will be done via visual styling
            return CurrentExampleEnglish;
        }
    }

    /// <summary>
    /// Gets the highlighted example text with only the Danish word bolded and underlined
    /// </summary>
    private FormattedExampleText _highlightedExampleDanish = new();
    public FormattedExampleText HighlightedExampleDanish
    {
        get => _highlightedExampleDanish;
        private set => SetProperty(ref _highlightedExampleDanish, value);
    }

    /// <summary>
    /// Gets the highlighted example text with only the English word bolded and underlined
    /// </summary>
    private FormattedExampleText _highlightedExampleEnglish = new();
    public FormattedExampleText HighlightedExampleEnglish
    {
        get => _highlightedExampleEnglish;
        private set => SetProperty(ref _highlightedExampleEnglish, value);
    }

    /// <summary>
    /// Highlights a specific word in text by finding and marking it
    /// Uses smart word matching to find different forms of the word
    /// </summary>
    private FormattedExampleText HighlightWord(string text, string word)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word))
            return new FormattedExampleText { FullText = text };

        // Handle multi-word expressions like "to destroy / to ruin / to break"
        var keywords = ExtractKeywords(word);
        
        System.Diagnostics.Debug.WriteLine($"[HighlightWord] Input word: '{word}' -> Keywords: {string.Join(", ", keywords)}");
        
        // Try to find any of the keywords in the text
        foreach (var keyword in keywords)
        {
            var result = FindAndHighlightWord(text, keyword);
            if (!string.IsNullOrEmpty(result.HighlightedWord))
            {
                System.Diagnostics.Debug.WriteLine($"[HighlightWord] Found highlight: '{result.HighlightedWord}' in text");
                return result;
            }
        }

        // If no keywords found, return the full text without highlighting
        // This ensures the text is still visible even if highlighting fails
        System.Diagnostics.Debug.WriteLine($"[HighlightWord] No highlight found for keywords: {string.Join(", ", keywords)}");
        return new FormattedExampleText { FullText = text };
    }

    /// <summary>
    /// Extracts keywords from a word expression (e.g., "to destroy / to ruin / to break" -> ["destroy", "ruin", "break"])
    /// Also handles multi-word phrases like "at skabe" -> ["skabe"]
    /// Handles noun articles like "en ulempe" -> ["ulempe"]
    /// </summary>
    private List<string> ExtractKeywords(string word)
    {
        var keywords = new List<string>();
        
        // Split by "/" to handle alternatives like "to destroy / to ruin / to break"
        var parts = word.Split(new[] { '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            
            // Split multi-word phrases and process each word separately
            var words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Language-specific prefixes and articles to skip
            var prefixesToSkip = new[] { "to", "at", "de", "for", "en", "et" };
            
            foreach (var singleWord in words)
            {
                var lowerWord = singleWord.ToLowerInvariant();
                
                // Skip single-letter words and common prefixes/articles
                if (lowerWord.Length <= 1 || Array.Exists(prefixesToSkip, p => p == lowerWord))
                    continue;
                
                var rootWord = ExtractRootWord(singleWord);
                
                if (!string.IsNullOrEmpty(rootWord) && !keywords.Contains(rootWord, StringComparer.OrdinalIgnoreCase))
                    keywords.Add(rootWord);
            }
        }

        return keywords;
    }

    /// <summary>
    /// Extracts the root word from a word by removing common suffixes
    /// </summary>
    private string ExtractRootWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        var lowerWord = word.ToLowerInvariant();
        return lowerWord;
    }

    /// <summary>
    /// Finds and highlights a word in text using flexible matching
    /// Priority: whole word form match > exact match > case-insensitive match
    /// </summary>
    private FormattedExampleText FindAndHighlightWord(string text, string keyword)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
            return new FormattedExampleText { FullText = text };

        var lowerText = text.ToLowerInvariant();
        var lowerKeyword = keyword.ToLowerInvariant();

        // Generate variants sorted by length (longest first) to match whole words
        var variants = GenerateWordVariants(lowerKeyword);
        
        System.Diagnostics.Debug.WriteLine($"[FindAndHighlightWord] Keyword: '{lowerKeyword}' -> Variants: {string.Join(", ", variants)}");
        
        // Try to find any variant as a whole word (with word boundaries)
        foreach (var variant in variants)
        {
            var (index, length) = FindWholeWord(lowerText, variant);
            if (index >= 0)
            {
                System.Diagnostics.Debug.WriteLine($"[FindAndHighlightWord] Found variant '{variant}' at index {index} with length {length}");
                return ExtractHighlightedText(text, index, length);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[FindAndHighlightWord] Variant '{variant}' not found as whole word");
            }
        }

        // If still no match, return full text (don't hide it)
        System.Diagnostics.Debug.WriteLine($"[FindAndHighlightWord] No variant found for keyword '{keyword}'");
        return new FormattedExampleText { FullText = text };
    }

    /// <summary>
    /// Finds a whole word in text with word boundaries
    /// Returns the index and length of the found word, or (-1, 0) if not found
    /// </summary>
    private (int index, int length) FindWholeWord(string text, string word)
    {
        int index = 0;
        while ((index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            // Check if it's a whole word (has word boundaries before and after)
            bool isStartBoundary = (index == 0) || !char.IsLetterOrDigit(text[index - 1]);
            bool isEndBoundary = (index + word.Length >= text.Length) || !char.IsLetterOrDigit(text[index + word.Length]);

            if (isStartBoundary && isEndBoundary)
            {
                return (index, word.Length);
            }

            index += 1; // Move past this match to find the next occurrence
        }

        return (-1, 0); // Not found as a whole word
    }

    /// <summary>
    /// Generates common word variants (e.g., "holdning" -> ["holdninger", "holdning"])
    /// Longer variants first to match whole word forms over base forms
    /// </summary>
    private List<string> GenerateWordVariants(string word)
    {
        var variants = new List<string>();
        
        // Add Danish plural forms (longer first for priority)
        // Always add +er and +s variants regardless of word ending
        variants.Add(word + "er"); // holdning -> holdninger, ulempe -> ulemper
        variants.Add(word + "ne"); // Danish definite plural
        
        if (!word.EndsWith("e"))
        {
            variants.Add(word + "e"); // Danish definite form (for words not ending in 'e')
        }
        
        // Add English-style variants
        variants.Add(word + "s");
        variants.Add(word + "ing");
        variants.Add(word + "ed");
        variants.Add(word + "est");
        
        // Add the base word (lowest priority)
        variants.Add(word);
        
        // Add variants with vowel handling for 'e' ending words
        if (word.EndsWith("e"))
        {
            var withoutE = word.Substring(0, word.Length - 1);
            variants.Add(withoutE + "ing");
            variants.Add(withoutE + "ed");
        }
        
        // For words ending in consonant, double the consonant before adding suffix
        if (word.Length > 1 && !IsVowel(word[word.Length - 1]) && IsVowel(word[word.Length - 2]))
        {
            variants.Add(word + word[word.Length - 1] + "ing");
            variants.Add(word + word[word.Length - 1] + "ed");
        }

        // Remove duplicates and sort by length (longest first)
        var result = variants.Distinct(StringComparer.OrdinalIgnoreCase)
                      .OrderByDescending(v => v.Length)
                      .ToList();
        
        System.Diagnostics.Debug.WriteLine($"[GenerateWordVariants] Word: '{word}' -> Variants: [{string.Join(", ", result)}]");
        
        return result;
    }

    /// <summary>
    /// Determines if a character is a vowel
    /// </summary>
    private bool IsVowel(char c)
    {
        return "aeiouAEIOU".Contains(c);
    }

    /// <summary>
    /// Extracts the highlighted text at a specific position
    /// </summary>
    private FormattedExampleText ExtractHighlightedText(string text, int index, int length)
    {
        return new FormattedExampleText
        {
            BeforeWord = text.Substring(0, index),
            HighlightedWord = text.Substring(index, length),
            AfterWord = text.Substring(index + length),
            FullText = text
        };
    }

    /// <summary>
    /// Updates the highlighted examples based on the current flashcard
    /// For Danish examples, tries to highlight the main word first, then the conjugation if available
    /// </summary>
    private void UpdateHighlightedExamples()
    {
        if (string.IsNullOrEmpty(CurrentExampleDanish) || string.IsNullOrEmpty(CurrentDanish))
            HighlightedExampleDanish = new FormattedExampleText { FullText = CurrentExampleDanish ?? string.Empty };
        else
        {
            // Try to highlight the main Danish word first
            var result = HighlightWord(CurrentExampleDanish, CurrentDanish);
            
            // If main word wasn't found and conjugation exists, try to highlight the conjugation
            if (string.IsNullOrEmpty(result.HighlightedWord) && !string.IsNullOrEmpty(CurrentConjugation))
            {
                result = HighlightWord(CurrentExampleDanish, CurrentConjugation);
            }
            
            HighlightedExampleDanish = result;
        }

        if (string.IsNullOrEmpty(CurrentExampleEnglish) || string.IsNullOrEmpty(CurrentEnglish))
            HighlightedExampleEnglish = new FormattedExampleText { FullText = CurrentExampleEnglish ?? string.Empty };
        else
            HighlightedExampleEnglish = HighlightWord(CurrentExampleEnglish, CurrentEnglish);
    }

    public bool HasFlashcards => CurrentFlashcard is not null;

    public bool IsAudioPlaying
    {
        get => _isAudioPlaying;
        private set
        {
            if (SetProperty(ref _isAudioPlaying, value))
            {
                OnPropertyChanged(nameof(CanNavigateFlashcards));
            }
        }
    }

    public bool CanNavigateFlashcards => !_isAudioPlaying;

    public string RotationMessage { get; } = "Randomly rotates every 15 seconds";

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

    public bool IsEditMode { get; private set; }

    public string NewDanish
    {
        get => _newDanish;
        set => SetProperty(ref _newDanish, value);
    }

    public string NewEnglish
    {
        get => _newEnglish;
        set => SetProperty(ref _newEnglish, value);
    }

    public string NewType
    {
        get => _newType;
        set => SetProperty(ref _newType, value);
    }

    public string NewConjugation
    {
        get => _newConjugation;
        set => SetProperty(ref _newConjugation, value);
    }

    public string NewExampleDanish
    {
        get => _newExampleDanish;
        set => SetProperty(ref _newExampleDanish, value);
    }

    public string NewExampleEnglish
    {
        get => _newExampleEnglish;
        set => SetProperty(ref _newExampleEnglish, value);
    }

    public string NewContextualTip
    {
        get => _newContextualTip;
        set => SetProperty(ref _newContextualTip, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value))
            {
                OnPropertyChanged(nameof(HasValidationMessage));
            }
        }
    }

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

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

     public string SearchText
     {
         get => _searchText;
         set
         {
             var newValue = value ?? string.Empty;
             // Only update if the value actually changed
             if (_searchText != newValue)
             {
                 _searchText = newValue;
                 OnPropertyChanged(nameof(SearchText));
                 UpdateSearchResults();
             }
         }
     }

    public ObservableCollection<FlashcardEntry> SearchResults
    {
        get => _searchResults;
        private set => SetProperty(ref _searchResults, value);
    }

    public bool IsMuted
    {
        get => _isMuted;
        private set
        {
            if (SetProperty(ref _isMuted, value))
            {
                OnPropertyChanged(nameof(MuteIcon));
                OnPropertyChanged(nameof(MuteTooltip));
            }
        }
    }

    public string MuteIcon => IsMuted ? "M" : "S";

    public string MuteTooltip => IsMuted ? "Unmute translations" : "Mute translations";

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public WritingEntry? CurrentWritingEntry
    {
        get => _currentWritingEntry;
        private set
        {
            if (SetProperty(ref _currentWritingEntry, value))
            {
                OnPropertyChanged(nameof(CurrentDanishWritingTitle));
                OnPropertyChanged(nameof(CurrentDanishWriting));
                OnPropertyChanged(nameof(CurrentEnglishWritingTitle));
                OnPropertyChanged(nameof(CurrentEnglishWriting));
                OnPropertyChanged(nameof(HasWritingEntries));
            }
        }
    }

    public string CurrentDanishWritingTitle => CurrentWritingEntry?.DanishWritingTitle ?? "Danish Writing";
    public string CurrentDanishWriting => CurrentWritingEntry?.DanishWriting ?? "No writing exercises available";
    public string CurrentEnglishWritingTitle => CurrentWritingEntry?.EnglishWritingTitle ?? "English Writing";
    public string CurrentEnglishWriting => CurrentWritingEntry?.EnglishWriting ?? string.Empty;

    public bool HasWritingEntries => CurrentWritingEntry is not null;

    public bool IsAddWritingPage
    {
        get => _isAddWritingPage;
        private set
        {
            if (SetProperty(ref _isAddWritingPage, value))
                OnPropertyChanged(nameof(IsWritingPage));
        }
    }

    public bool IsWritingPage => !IsAddWritingPage;

    public string NewWritingDanish
    {
        get => _newWritingDanish;
        set => SetProperty(ref _newWritingDanish, value);
    }

    public string NewWritingEnglish
    {
        get => _newWritingEnglish;
        set => SetProperty(ref _newWritingEnglish, value);
    }

    public string NewWritingDanishTitle
    {
        get => _newWritingDanishTitle;
        set => SetProperty(ref _newWritingDanishTitle, value);
    }

    public string NewWritingEnglishTitle
    {
        get => _newWritingEnglishTitle;
        set => SetProperty(ref _newWritingEnglishTitle, value);
    }

    public MainWindowViewModel()
    {
        ShowAddRecordCommand = new RelayCommand(ShowAddRecordPage);
        ShowEditRecordCommand = new RelayCommand(ShowEditRecord);
        SaveNewRecordCommand = new RelayCommand(SaveNewRecord);
        CancelAddRecordCommand = new RelayCommand(CancelAddRecord);
        ShowAddWritingCommand = new RelayCommand(ShowAddWritingPage);
        ShowEditWritingCommand = new RelayCommand(ShowEditWriting);
        SaveNewWritingCommand = new RelayCommand(SaveNewWriting);
        CancelAddWritingCommand = new RelayCommand(CancelAddWriting);
        NextWritingCommand = new RelayCommand(SelectNextWritingEntry);
        PreviousWritingCommand = new RelayCommand(SelectPreviousWritingEntry);
        PlayCommand = new RelayCommand(Play);
        PauseCommand = new RelayCommand(Pause);
        PreviousCardCommand = new RelayCommand(SelectPreviousFlashcard);
        NextCardCommand = new RelayCommand(SelectNextFlashcard);
        PlayDanishWordCommand = new AsyncRelayCommand(PlayCurrentDanishWord);
        PlayDanishExampleCommand = new AsyncRelayCommand(PlayCurrentDanishExample);
        MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
        SelectSearchResultCommand = new RelayCommand<FlashcardEntry?>(SelectSearchResult);
        ToggleMuteCommand = new RelayCommand(ToggleMute);

        _rotationTimer = new DispatcherTimer
        {
            Interval = RotationInterval,
        };
        _rotationTimer.Tick += OnRotationTimerTick;

        SelectNextFlashcard();
        UpdateRotationState();
        SelectFirstWritingEntry();
    }

    public void Dispose()
    {
        _rotationTimer.Stop();
        _rotationTimer.Tick -= OnRotationTimerTick;
        _audioService?.Dispose();
    }

    /// <summary>
    /// Updates search results based on the current search text
    /// Performs a case-insensitive "like" search on Danish words
    /// </summary>
    private void UpdateSearchResults()
    {
        SearchResults.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        var searchLower = SearchText.Trim().ToLowerInvariant();
        
        // Filter flashcards by Danish text containing the search text (case-insensitive)
        var results = _flashcards
            .Where(fc => fc.Danish.ToLowerInvariant().Contains(searchLower))
            .OrderBy(fc => fc.Danish) // Sort alphabetically for better UX
            .ToList();

        foreach (var result in results)
        {
            SearchResults.Add(result);
        }
    }

    /// <summary>
    /// Handles selection of a search result
    /// Sets it as the current flashcard and clears the search
    /// </summary>
    private void SelectSearchResult(FlashcardEntry? flashcard)
    {
        if (flashcard is not null)
        {
            CurrentFlashcard = flashcard;
            // Defer clearing the search text to avoid AutoCompleteBox issues
            // Use Dispatcher to clear it after the current event is processed
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                SearchText = string.Empty;
            }, DispatcherPriority.Input);
        }
    }

    private void OnRotationTimerTick(object? sender, EventArgs e)
    {
        SelectNextFlashcard();
    }

    private void ShowAddRecordPage()
    {
        ResetForm();
        IsAddRecordPage = true;
        IsEditMode = false;
    }

    private void ShowEditRecord()
    {
        if (CurrentFlashcard is null) return;

        // Populate form with current values and switch to edit mode
        NewDanish = CurrentFlashcard.Danish;
        NewEnglish = CurrentFlashcard.English;
        NewType = CurrentFlashcard.Type ?? string.Empty;
        NewConjugation = CurrentFlashcard.Conjugation ?? string.Empty;
        NewExampleDanish = CurrentFlashcard.ExampleDanish ?? string.Empty;
        NewExampleEnglish = CurrentFlashcard.ExampleEnglish ?? string.Empty;
        NewContextualTip = CurrentFlashcard.ContextualTip ?? string.Empty;

        IsEditMode = true;
        IsAddRecordPage = true;
    }

    private void SaveNewRecord()
    {
        var danish = NewDanish.Trim();
        var english = NewEnglish.Trim();
        var type = string.IsNullOrWhiteSpace(NewType) ? null : NewType.Trim();
        var conjugation = string.IsNullOrWhiteSpace(NewConjugation) ? null : NewConjugation.Trim();
        var exampleDanish = string.IsNullOrWhiteSpace(NewExampleDanish) ? null : NewExampleDanish.Trim();
        var exampleEnglish = string.IsNullOrWhiteSpace(NewExampleEnglish) ? null : NewExampleEnglish.Trim();
        var contextualTip = string.IsNullOrWhiteSpace(NewContextualTip) ? null : NewContextualTip.Trim();

        if (string.IsNullOrWhiteSpace(danish) || string.IsNullOrWhiteSpace(english))
        {
            ValidationMessage = "Danish and English are required.";
            return;
        }

        var flashcard = new FlashcardEntry
        {
            Danish = danish,
            English = english,
            Type = type,
            Conjugation = conjugation,
            ExampleDanish = exampleDanish,
            ExampleEnglish = exampleEnglish,
            ContextualTip = contextualTip,
        };

        if (IsEditMode)
        {
            // Update existing record (use original CurrentFlashcard.Danish as key)
            var original = CurrentFlashcard?.Danish ?? string.Empty;
            if (!FlashcardDataSource.TryUpdateFlashcard(original, flashcard))
            {
                ValidationMessage = $"Unable to update: Danish '{danish}' conflicts with an existing record.";
                return;
            }

            CurrentFlashcard = flashcard;
            IsEditMode = false;
            ResetForm();
            IsAddRecordPage = false;
        }
        else
        {
            if (!FlashcardDataSource.TryAddFlashcard(flashcard))
            {
                ValidationMessage = $"A record with Danish '{danish}' already exists.";
                return;
            }

            CurrentFlashcard = flashcard;

            ResetForm();
            IsAddRecordPage = false;
        }
    }

    private void CancelAddRecord()
    {
        ResetForm();
        IsAddRecordPage = false;
        IsEditMode = false;
    }

    private void ShowAddWritingPage()
    {
        ResetForm();
        IsAddWritingPage = true;
        IsEditMode = false;
    }

    private void ShowEditWriting()
    {
        if (CurrentWritingEntry is null) return;

        NewWritingDanishTitle = CurrentWritingEntry.DanishWritingTitle;
        NewWritingDanish = CurrentWritingEntry.DanishWriting;
        NewWritingEnglishTitle = CurrentWritingEntry.EnglishWritingTitle;
        NewWritingEnglish = CurrentWritingEntry.EnglishWriting;

        IsEditMode = true;
        IsAddWritingPage = true;
    }

    private void SaveNewWriting()
    {
        var danishTitle = NewWritingDanishTitle.Trim();
        var danish = NewWritingDanish.Trim();
        var englishTitle = NewWritingEnglishTitle.Trim();
        var english = NewWritingEnglish.Trim();

        if (string.IsNullOrWhiteSpace(danish) || string.IsNullOrWhiteSpace(english))
        {
            ValidationMessage = "Danish and English are required.";
            return;
        }

        var writingEntry = new WritingEntry
        {
            DanishWritingTitle = danishTitle,
            DanishWriting = danish,
            EnglishWritingTitle = englishTitle,
            EnglishWriting = english,
        };

        if (IsEditMode)
        {
            // Update existing writing entry
            var original = CurrentWritingEntry?.DanishWriting ?? string.Empty;
            if (!WritingDataSource.TryUpdateWritingEntry(original, writingEntry))
            {
                ValidationMessage = $"Unable to update: Danish '{danish}' conflicts with an existing record.";
                return;
            }

            CurrentWritingEntry = writingEntry;
            IsEditMode = false;
            ResetForm();
            IsAddWritingPage = false;
        }
        else
        {
            if (!WritingDataSource.TryAddWritingEntry(writingEntry))
            {
                ValidationMessage = $"A record with Danish '{danish}' already exists.";
                return;
            }

            CurrentWritingEntry = writingEntry;

            ResetForm();
            IsAddWritingPage = false;
        }
    }

    private void CancelAddWriting()
    {
        ResetForm();
        IsAddWritingPage = false;
        IsEditMode = false;
    }

    private void ResetForm()
    {
        NewDanish = string.Empty;
        NewEnglish = string.Empty;
        NewType = string.Empty;
        NewConjugation = string.Empty;
        NewExampleDanish = string.Empty;
        NewExampleEnglish = string.Empty;
        NewContextualTip = string.Empty;
        NewWritingDanish = string.Empty;
        NewWritingEnglish = string.Empty;
        NewWritingDanishTitle = string.Empty;
        NewWritingEnglishTitle = string.Empty;
        ValidationMessage = string.Empty;
    }

    private void UpdateRotationState()
    {
        if (IsAddRecordPage || _flashcards.Count <= 1 || IsRotationPaused)
        {
            _rotationTimer.Stop();
            return;
        }

        if (!_rotationTimer.IsEnabled)
        {
            _rotationTimer.Start();
        }
    }

    private void SelectNextFlashcard()
    {
        if (_flashcards.Count == 0)
        {
            CurrentFlashcard = null;
            return;
        }

        // Store the current flashcard in the history before moving to the next one
        if (CurrentFlashcard is not null)
        {
            _navigationHistory.Push(CurrentFlashcard);
        }

        if (_flashcards.Count == 1)
        {
            CurrentFlashcard = _flashcards[0];
            return;
        }

        FlashcardEntry nextFlashcard;

        do
        {
            nextFlashcard = _flashcards[_random.Next(_flashcards.Count)];
        }
        while (ReferenceEquals(nextFlashcard, CurrentFlashcard));

        CurrentFlashcard = nextFlashcard;
    }

    private void SelectPreviousFlashcard()
    {
        if (_navigationHistory.Count == 0)
        {
            // No previous flashcard in history
            return;
        }

        // Pop the latest entry from the navigation history
        var previousFlashcard = _navigationHistory.Pop();

        if (ReferenceEquals(previousFlashcard, CurrentFlashcard))
        {
            // If the popped flashcard is the same as the current one, continue popping
            SelectPreviousFlashcard();
            return;
        }

        // Set the current flashcard to the previous one
        CurrentFlashcard = previousFlashcard;
    }

    private void SelectFirstWritingEntry()
    {
        if (_writingEntries.Count > 0)
        {
            CurrentWritingEntry = _writingEntries[0];
        }
    }

    private void SelectNextWritingEntry()
    {
        if (_writingEntries.Count == 0)
        {
            CurrentWritingEntry = null;
            return;
        }

        if (_writingEntries.Count == 1)
        {
            CurrentWritingEntry = _writingEntries[0];
            return;
        }

        var currentIndex = -1;
        for (int i = 0; i < _writingEntries.Count; i++)
        {
            if (ReferenceEquals(_writingEntries[i], CurrentWritingEntry))
            {
                currentIndex = i;
                break;
            }
        }

        var nextIndex = currentIndex >= _writingEntries.Count - 1 ? 0 : currentIndex + 1;
        CurrentWritingEntry = _writingEntries[nextIndex];
    }

    private void SelectPreviousWritingEntry()
    {
        if (_writingEntries.Count == 0)
        {
            CurrentWritingEntry = null;
            return;
        }

        // Find the index of the current entry
        var currentIndex = -1;
        for (int i = 0; i < _writingEntries.Count; i++)
        {
            if (ReferenceEquals(_writingEntries[i], CurrentWritingEntry))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex <= 0)
        {
            // If at the beginning or not found, wrap to the end
            CurrentWritingEntry = _writingEntries[_writingEntries.Count - 1];
        }
        else
        {
            CurrentWritingEntry = _writingEntries[currentIndex - 1];
        }
    }

    private void Play()
    {
        IsRotationPaused = false;
    }

    private void Pause()
    {
        IsRotationPaused = true;
    }

    private async Task PlayCurrentDanishWord()
    {
        if (CurrentFlashcard is null)
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayCurrentDanishWord: No current flashcard");
            return;
        }

        try
        {
            IsAudioPlaying = true;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayCurrentDanishWord: Playing '{CurrentFlashcard.Danish}'");
            await _audioService.PlayDanishPronunciation(CurrentFlashcard.Danish);
        }
        finally
        {
            IsAudioPlaying = false;
        }
    }

    private async Task PlayCurrentDanishExample()
    {
        if (CurrentFlashcard is null || string.IsNullOrWhiteSpace(CurrentFlashcard.ExampleDanish))
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayCurrentDanishExample: No example available");
            return;
        }

        try
        {
            IsAudioPlaying = true;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayCurrentDanishExample: Playing '{CurrentFlashcard.ExampleDanish}'");
            await _audioService.PlayDanishPronunciation(CurrentFlashcard.ExampleDanish);
        }
        finally
        {
            IsAudioPlaying = false;
        }
    }

    /// <summary>
    /// Plays the Danish word twice, waits 1 second, plays English once, then plays Danish example once
    /// Called automatically when a new flashcard is loaded
    /// Only plays if not muted
    /// </summary>
    private async Task PlayDanishWordTwiceAsync()
    {
        if (IsMuted)
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: Skipped - audio is muted");
            return;
        }

        if (CurrentFlashcard is null || string.IsNullOrWhiteSpace(CurrentFlashcard.Danish))
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: No Danish word available");
            return;
        }

        try
        {
            IsAudioPlaying = true;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Starting playback sequence");
            
            // Play Danish word first time
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Playing Danish '{CurrentFlashcard.Danish}' (1st time)");
            await _audioService.PlayDanishPronunciation(CurrentFlashcard.Danish);
            
            // Wait a brief moment between repeats
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            
            // Play Danish word second time
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Playing Danish '{CurrentFlashcard.Danish}' (2nd time)");
            await _audioService.PlayDanishPronunciation(CurrentFlashcard.Danish);
            
            // Wait 1 second before playing English
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: Waiting 1 second before English playback");
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            // Play English translation once
            if (!string.IsNullOrWhiteSpace(CurrentFlashcard.English))
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Playing English '{CurrentFlashcard.English}'");
                await _audioService.PlayEnglishPronunciation(CurrentFlashcard.English);
                System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: English playback complete");
            }
            
            // Wait 500ms before playing Danish example
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            
            // Play Danish example once
            if (!string.IsNullOrWhiteSpace(CurrentFlashcard.ExampleDanish))
            {
                System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Playing Danish example '{CurrentFlashcard.ExampleDanish}'");
                await _audioService.PlayDanishPronunciation(CurrentFlashcard.ExampleDanish);
                System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: Danish example playback complete");
            }
            
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayDanishWordTwiceAsync: Complete playback sequence finished");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayDanishWordTwiceAsync: Error - {ex.Message}");
        }
        finally
        {
            IsAudioPlaying = false;
        }
    }

    private void MinimizeToTray()
    {
        // This method will be called when the close button is clicked
        // The actual minimization to tray will be handled in the MainWindow code-behind
        System.Diagnostics.Debug.WriteLine("[ViewModel] MinimizeToTray command executed");
    }

    /// <summary>
    /// Toggles the mute state for automatic audio playback
    /// </summary>
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        System.Diagnostics.Debug.WriteLine($"[ViewModel] ToggleMute: Audio is now {(IsMuted ? "muted" : "unmuted")}");
    }
}