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
    private readonly IReadOnlyList<SpeakingEntry> _speakingEntries = SpeakingDataSource.GetEntries();
    private readonly DispatcherTimer _rotationTimer;
    private readonly Random _random = new();
    private readonly AudioService _audioService = new();
    private readonly Stack<FlashcardEntry> _navigationHistory = new();
    private FlashcardEntry? _currentFlashcard;
    private WritingEntry? _currentWritingEntry;
    private SpeakingEntry? _currentSpeakingEntry;
    private bool _isAddRecordPage;
    private bool _isAddWritingPage;
    private bool _isAddSpeakingPage;
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
    private string _newSpeakingTopic = string.Empty;
    private string _newSpeakingTopicTitle = string.Empty;
    private string _newSpeakingNotes = string.Empty;
    private string _newSpeakingNotesTitle = string.Empty;
    private string _validationMessage = string.Empty;
    private string _searchText = string.Empty;
    private bool _isAudioPlaying = false;

    // Speaking timer
    private DispatcherTimer? _speakingTimer;
    private int _speakingTimerSeconds = 120;
    private bool _isSpeakingTimerRunning = false;

    // Speaking word-highlight
    private DispatcherTimer? _speakingWordTimer;
    private int _currentSpeakingWordIndex = -1;
    private int _speakingTotalWords = 0;
    private List<string> _speakingWords = new();

    // MBSP
    private readonly IReadOnlyList<MbspQuestion> _mbspQuestions = MbspDataSource.GetQuestions();
    private MbspQuestion? _currentMbspQuestion;
    private int _currentMbspIndex = -1;
    // History-based navigation: _mbspHistory records indices of questions shown in order.
    // _mbspHistoryPosition is the current cursor into that list.
    // _mbspRemainingQueue is a shuffled pool of not-yet-shown questions.
    // _mbspAnswerHistory persists the answer the user gave for each question index.
    private List<int> _mbspHistory = new();
    private int _mbspHistoryPosition = -1;
    private List<int> _mbspRemainingQueue = new();
    private Dictionary<int, string> _mbspAnswerHistory = new();
    private bool _isAddMbspPage;
    private string _selectedMbspPeriod = "All";
    private bool _mbspAnswerRevealed;
    private string? _mbspSelectedChoice;
    private string _newMbspQuestion = string.Empty;
    private string _newMbspQuestionEnglish = string.Empty;
    private string _newMbspChoiceA = string.Empty;
    private string _newMbspChoiceAEnglish = string.Empty;
    private string _newMbspChoiceB = string.Empty;
    private string _newMbspChoiceBEnglish = string.Empty;
    private string _newMbspChoiceC = string.Empty;
    private string _newMbspChoiceCEnglish = string.Empty;
    private string _newMbspCorrectChoice = string.Empty; // stores the actual answer text
    private string _newMbspPeriod = string.Empty;
    private bool _mbspShowEnglish = false;
    private int _mbspCorrectCount = 0;
    private string _selectedTypeFilter = "All";
    private readonly HashSet<FlashcardEntry> _flashcardsViewedInSession = new();

    // Search autocomplete collection
    private ObservableCollection<FlashcardEntry> _searchResults = new();

    public string DataSourceName { get; } = "Local in-solution data source";

    public IReadOnlyList<string> TypeFilters { get; } = new[] { "All", "v.", "adj.", "n.", "adv.", "prep.", "conj." };

    public bool IsTypeFilterVisible => _selectedTabIndex == 0 || _selectedTabIndex == 1;

    public IReadOnlyList<string> SpeakingCategories { get; } = new[] { "2 Min. Presentation" };

    private string _selectedSpeakingCategory = "2 Min. Presentation";
    public string SelectedSpeakingCategory
    {
        get => _selectedSpeakingCategory;
        set
        {
            if (SetProperty(ref _selectedSpeakingCategory, value))
            {
                ResetSpeakingTimer();
                OnPropertyChanged(nameof(IsSpeakingTimerVisible));
            }
        }
    }

    public bool IsSpeakingTimerVisible => _selectedSpeakingCategory == "2 Min. Presentation";

    public string SpeakingTimerDisplay =>
        $"{_speakingTimerSeconds / 60:D2}:{_speakingTimerSeconds % 60:D2}";

    public bool IsSpeakingTimerRunning
    {
        get => _isSpeakingTimerRunning;
        private set
        {
            if (SetProperty(ref _isSpeakingTimerRunning, value))
                OnPropertyChanged(nameof(IsSpeakingTimerStopped));
        }
    }

    public bool IsSpeakingTimerStopped => !IsSpeakingTimerRunning;

    public IAsyncRelayCommand StartSpeakingTimerCommand { get; }

    public string SelectedTypeFilter
    {
        get => _selectedTypeFilter;
        set
        {
            if (SetProperty(ref _selectedTypeFilter, value))
            {
                _navigationHistory.Clear();
                _flashcardsViewedInSession.Clear();
                SelectNextFlashcard();
            }
        }
    }

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
    public IRelayCommand ShowAddSpeakingCommand { get; }
    public IRelayCommand SaveNewSpeakingCommand { get; }
    public IRelayCommand CancelAddSpeakingCommand { get; }
    public IRelayCommand ShowEditSpeakingCommand { get; }
    public IRelayCommand NextSpeakingCommand { get; }
    public IRelayCommand PreviousSpeakingCommand { get; }
    public IRelayCommand PlayCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand PreviousCardCommand { get; }
    public IRelayCommand NextCardCommand { get; }
    public IAsyncRelayCommand PlayDanishWordCommand { get; }
    public IAsyncRelayCommand PlayDanishExampleCommand { get; }
    public IAsyncRelayCommand PlaySpeakingTopicCommand { get; }
    public IRelayCommand MinimizeToTrayCommand { get; set; }
    public IRelayCommand<FlashcardEntry?> SelectSearchResultCommand { get; }
    public IRelayCommand ToggleMuteCommand { get; }
    public IRelayCommand<string> SelectTabCommand { get; }

    // MBSP commands
    public IRelayCommand NextMbspCommand { get; }
    public IRelayCommand PreviousMbspCommand { get; }
    public IRelayCommand<string> SelectMbspChoiceCommand { get; }
    public IRelayCommand RevealMbspAnswerCommand { get; }
    public IRelayCommand ShowAddMbspCommand { get; }
    public IRelayCommand ShowEditMbspCommand { get; }
    public IRelayCommand SaveMbspCommand { get; }
    public IRelayCommand CancelMbspCommand { get; }
    public IRelayCommand<string> SelectMbspCorrectChoiceCommand { get; }

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
                
                // Track viewed flashcards for session counter
                if (value is not null)
                    _flashcardsViewedInSession.Add(value);
                OnPropertyChanged(nameof(FlashcardProgressText));

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

        // First, try to match multi-word phrases directly (e.g. "at gå ud over" -> try "gå ud over" first)
        var phrases = ExtractPhrases(word);
        foreach (var phrase in phrases)
        {
            var result = FindAndHighlightPhrase(text, phrase);
            if (!string.IsNullOrEmpty(result.HighlightedWord))
            {
                System.Diagnostics.Debug.WriteLine($"[HighlightWord] Found phrase highlight: '{result.HighlightedWord}' in text");
                return result;
            }
        }

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
    /// Extracts multi-word phrases from a word expression, removing leading articles/prefixes.
    /// E.g. "at gå ud over" -> ["gå ud over"], "to give up / to surrender" -> ["give up", "surrender"]
    /// Only returns phrases with more than one word (single words handled by ExtractKeywords).
    /// </summary>
    private List<string> ExtractPhrases(string word)
    {
        var phrases = new List<string>();
        var prefixesToSkip = new[] { "to", "at", "de", "for", "en", "et" };

        var parts = word.Split(new[] { '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var words = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Strip leading prefix if any
            int start = 0;
            if (words.Length > 0 && Array.Exists(prefixesToSkip, p => p.Equals(words[0], StringComparison.OrdinalIgnoreCase)))
                start = 1;

            if (words.Length - start > 1)
            {
                var phrase = string.Join(" ", words.Skip(start));
                if (!phrases.Contains(phrase, StringComparer.OrdinalIgnoreCase))
                    phrases.Add(phrase);
            }
        }

        return phrases;
    }

    /// <summary>
    /// Finds and highlights a multi-word phrase in text using case-insensitive search.
    /// </summary>
    private FormattedExampleText FindAndHighlightPhrase(string text, string phrase)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(phrase))
            return new FormattedExampleText { FullText = text };

        int index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return ExtractHighlightedText(text, index, phrase.Length);
        }

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

    /// <summary>Counter text "X of Y" showing unique flashcards seen vs total in current filter.</summary>
    public string FlashcardProgressText =>
        $"{_flashcardsViewedInSession.Count} of {GetFilteredFlashcards().Count}";

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
        set
        {
            if (SetProperty(ref _selectedTabIndex, value))
            {
                OnPropertyChanged(nameof(IsTypeFilterVisible));
            }
        }
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
                OnPropertyChanged(nameof(CurrentDanishWritingSegments));
                OnPropertyChanged(nameof(CurrentEnglishWritingTitle));
                OnPropertyChanged(nameof(CurrentEnglishWriting));
                OnPropertyChanged(nameof(HasWritingEntries));
            }
        }
    }

    private static readonly string[] GreenPhrases =
    {
        "Diagrammet viser", "andelen af", "Det fremgår tydeligt", "Herefter følger",
        "Der kan være flere årsager til disse markante forskelle", "For det første",
        "For det andet", "Spørgsmålet om", "På den ene side", "På den anden side",
        "Desuden", " er det vigtigt at understrege", "Sammenfattende mener jeg",
        "Tak for din mail", "Tusind tak for din mail", "Det var rigtig hyggeligt at", "høre fra dig",
        "Hvad angår", "Med hensyn til", "når det kommer til", "jeg vil meget gerne","svare på dine spørgsmål",
        "dele mine tanker med dig", "først og fremmest", "det kunne være superhyggeligt", "hvis vi mødtes snart",
        "så vi kan vende det hele over en kop kaffe", "Lad mig vide","hvornår det passer dig bedst",
        "Hvor er det dejligt at høre, at", "Hvor er det spændende med", "Hvor lyder det spændende med", "derimod",
        "er komplekst", "hvorvidt", "hvilke"
    };

    public string CurrentDanishWritingTitle => CurrentWritingEntry?.DanishWritingTitle ?? "Danish Writing";
    public string CurrentDanishWriting => CurrentWritingEntry?.DanishWriting ?? "No writing exercises available";

    public IReadOnlyList<WritingSegment> CurrentDanishWritingSegments =>
        BuildSegments(CurrentDanishWriting, GreenPhrases);

    private static IReadOnlyList<WritingSegment> BuildSegments(string text, string[] phrases)
    {
        var segments = new List<WritingSegment>();
        int pos = 0;
        while (pos < text.Length)
        {
            int earliest = -1;
            string? match = null;
            foreach (var phrase in phrases)
            {
                int idx = text.IndexOf(phrase, pos, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0 && (earliest < 0 || idx < earliest))
                {
                    earliest = idx;
                    match = phrase;
                }
            }
            if (match == null)
            {
                segments.Add(new WritingSegment { Text = text[pos..] });
                break;
            }
            if (earliest > pos)
                segments.Add(new WritingSegment { Text = text[pos..earliest] });
            segments.Add(new WritingSegment { Text = text[earliest..(earliest + match.Length)], IsHighlighted = true });
            pos = earliest + match.Length;
        }
        return segments;
    }
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

    // ─── Speaking Properties ────────────────────────────────────────────────────

    public SpeakingEntry? CurrentSpeakingEntry
    {
        get => _currentSpeakingEntry;
        private set
        {
            if (SetProperty(ref _currentSpeakingEntry, value))
            {
                OnPropertyChanged(nameof(CurrentSpeakingTopicTitle));
                OnPropertyChanged(nameof(CurrentSpeakingTopic));
                OnPropertyChanged(nameof(CurrentSpeakingNotesTitle));
                OnPropertyChanged(nameof(CurrentSpeakingNotes));
                OnPropertyChanged(nameof(HasSpeakingEntries));
                ResetWordHighlight();
            }
        }
    }

    public string CurrentSpeakingTopicTitle => _currentSpeakingEntry?.TopicTitle ?? "Topic";
    public string CurrentSpeakingTopic => _currentSpeakingEntry?.Topic ?? "No speaking entries available";
    public string CurrentSpeakingNotesTitle => _currentSpeakingEntry?.NotesTitle ?? "Notes";
    public string CurrentSpeakingNotes => _currentSpeakingEntry?.Notes ?? string.Empty;
    public bool HasSpeakingEntries => _currentSpeakingEntry is not null;

    public IReadOnlyList<WritingSegment> CurrentSpeakingTopicSegments =>
        BuildWordHighlightSegments(CurrentSpeakingTopic, _currentSpeakingWordIndex);

    /// <summary>Fraction (0–1) of words spoken so far. -1 when not active.</summary>
    public double SpeakingWordProgress =>
        _speakingTotalWords > 0
            ? (double)_currentSpeakingWordIndex / _speakingTotalWords
            : -1;

    public bool IsAddSpeakingPage
    {
        get => _isAddSpeakingPage;
        private set
        {
            if (SetProperty(ref _isAddSpeakingPage, value))
                OnPropertyChanged(nameof(IsSpeakingPage));
        }
    }

    public bool IsSpeakingPage => !IsAddSpeakingPage;

    public string NewSpeakingTopicTitle
    {
        get => _newSpeakingTopicTitle;
        set => SetProperty(ref _newSpeakingTopicTitle, value);
    }

    public string NewSpeakingTopic
    {
        get => _newSpeakingTopic;
        set => SetProperty(ref _newSpeakingTopic, value);
    }

    public string NewSpeakingNotesTitle
    {
        get => _newSpeakingNotesTitle;
        set => SetProperty(ref _newSpeakingNotesTitle, value);
    }

    public string NewSpeakingNotes
    {
        get => _newSpeakingNotes;
        set => SetProperty(ref _newSpeakingNotes, value);
    }

    // ─── MBSP Properties ───────────────────────────────────────────────────────

    public MbspQuestion? CurrentMbspQuestion
    {
        get => _currentMbspQuestion;
        private set
        {
            if (SetProperty(ref _currentMbspQuestion, value))
            {
                OnPropertyChanged(nameof(CurrentMbspQuestionText));
                OnPropertyChanged(nameof(CurrentMbspQuestionEnglish));
                OnPropertyChanged(nameof(HasCurrentMbspQuestionEnglish));
                OnPropertyChanged(nameof(CurrentMbspChoiceA));
                OnPropertyChanged(nameof(CurrentMbspChoiceAEnglish));
                OnPropertyChanged(nameof(HasCurrentMbspChoiceAEnglish));
                OnPropertyChanged(nameof(CurrentMbspChoiceB));
                OnPropertyChanged(nameof(CurrentMbspChoiceBEnglish));
                OnPropertyChanged(nameof(HasCurrentMbspChoiceBEnglish));
                OnPropertyChanged(nameof(CurrentMbspChoiceC));
                OnPropertyChanged(nameof(CurrentMbspChoiceCEnglish));
                OnPropertyChanged(nameof(HasCurrentMbspChoiceCEnglish));
                OnPropertyChanged(nameof(CurrentMbspHasChoiceC));
                OnPropertyChanged(nameof(HasMbspQuestions));
                MbspAnswerRevealed = false;
                MbspSelectedChoice = null;
            }
        }
    }

    public string CurrentMbspQuestionText => _currentMbspQuestion?.Question ?? "No questions available";
    public string? CurrentMbspQuestionEnglish => _currentMbspQuestion?.QuestionEnglish;
    public bool HasCurrentMbspQuestionEnglish => _currentMbspQuestion?.HasQuestionEnglish == true;
    public string CurrentMbspChoiceA => _currentMbspQuestion?.ChoiceA ?? string.Empty;
    public string? CurrentMbspChoiceAEnglish => _currentMbspQuestion?.ChoiceAEnglish;
    public bool HasCurrentMbspChoiceAEnglish => _currentMbspQuestion?.HasChoiceAEnglish == true;
    public string CurrentMbspChoiceB => _currentMbspQuestion?.ChoiceB ?? string.Empty;
    public string? CurrentMbspChoiceBEnglish => _currentMbspQuestion?.ChoiceBEnglish;
    public bool HasCurrentMbspChoiceBEnglish => _currentMbspQuestion?.HasChoiceBEnglish == true;
    public string? CurrentMbspChoiceC => _currentMbspQuestion?.ChoiceC;
    public string? CurrentMbspChoiceCEnglish => _currentMbspQuestion?.ChoiceCEnglish;
    public bool HasCurrentMbspChoiceCEnglish => _currentMbspQuestion?.HasChoiceCEnglish == true;
    public bool CurrentMbspHasChoiceC => _currentMbspQuestion?.HasChoiceC == true;
    public bool HasMbspQuestions => _currentMbspQuestion is not null;

    public bool MbspShowEnglish
    {
        get => _mbspShowEnglish;
        set => SetProperty(ref _mbspShowEnglish, value);
    }

    public bool MbspAnswerRevealed
    {
        get => _mbspAnswerRevealed;
        private set
        {
            if (SetProperty(ref _mbspAnswerRevealed, value))
            {
                OnPropertyChanged(nameof(MbspChoiceAResult));
                OnPropertyChanged(nameof(MbspChoiceBResult));
                OnPropertyChanged(nameof(MbspChoiceCResult));
            }
        }
    }

    public string? MbspSelectedChoice
    {
        get => _mbspSelectedChoice;
        private set
        {
            if (SetProperty(ref _mbspSelectedChoice, value))
            {
                OnPropertyChanged(nameof(MbspChoiceASelected));
                OnPropertyChanged(nameof(MbspChoiceBSelected));
                OnPropertyChanged(nameof(MbspChoiceCSelected));
                OnPropertyChanged(nameof(MbspChoiceAResult));
                OnPropertyChanged(nameof(MbspChoiceBResult));
                OnPropertyChanged(nameof(MbspChoiceCResult));
                OnPropertyChanged(nameof(MbspAnswerFeedback));
                OnPropertyChanged(nameof(MbspHasFeedback));
            }
        }
    }

    public bool MbspChoiceASelected => MbspSelectedChoice == "A";
    public bool MbspChoiceBSelected => MbspSelectedChoice == "B";
    public bool MbspChoiceCSelected => MbspSelectedChoice == "C";

    /// <summary>null = not revealed, "correct", "wrong", "highlight" (correct answer when wrong was chosen)</summary>
    public string? MbspChoiceAResult => GetChoiceResult("A");
    public string? MbspChoiceBResult => GetChoiceResult("B");
    public string? MbspChoiceCResult => GetChoiceResult("C");

    public string MbspAnswerFeedback
    {
        get
        {
            if (MbspSelectedChoice is null) return string.Empty;
            bool isCorrect = string.Equals(MbspSelectedChoice, _currentMbspQuestion?.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
            return isCorrect
                ? "✓ KORREKT!"
                : $"✗ FORKERT! Det rigtige svar er: {_currentMbspQuestion?.CorrectAnswer}";
        }
    }

    public bool MbspHasFeedback => !string.IsNullOrEmpty(MbspAnswerFeedback);

    public string MbspScoreText
    {
        get
        {
            var total = GetMbspTotal();
            return $"{_mbspCorrectCount} / {total}";
        }
    }

    /// <summary>Left counter: "CorrectCount | WrongCount"</summary>
    public string MbspCorrectWrongText =>
        $"{_mbspCorrectCount} | {_mbspAnswerHistory.Count - _mbspCorrectCount}";

    /// <summary>Right counter: "CurrentQuestionNumber of TotalQuestions"</summary>
    public string MbspQuestionProgressText =>
        $"{(_mbspHistoryPosition >= 0 ? _mbspHistoryPosition + 1 : 0)} of {GetMbspTotal()}";

    private int GetMbspTotal() => (_selectedMbspPeriod == "All")
        ? _mbspQuestions.Count
        : _mbspQuestions.Count(q => string.Equals(q.Period, _selectedMbspPeriod, StringComparison.Ordinal));

    /// <summary>True when every question in the current pool has been answered.</summary>
    public bool IsMbspComplete
    {
        get
        {
            var total = GetMbspTotal();
            return total > 0 && _mbspAnswerHistory.Count >= total;
        }
    }

    /// <summary>True when the Previous button should be enabled (not on the first question).</summary>
    public bool CanGoToPreviousMbsp => _mbspHistoryPosition > 0;

    /// <summary>True when the Next button should be enabled (not yet on the last question of the pool).</summary>
    public bool CanGoToNextMbsp => (_mbspHistoryPosition + 1) < GetMbspTotal();

    /// <summary>True when the test is complete AND the pass threshold (≥80 %) is met.</summary>
    public bool MbspResultIsPass => IsMbspComplete && _mbspCorrectCount >= GetMbspTotal() * 0.8;

    /// <summary>True when the test is complete AND the pass threshold was NOT met.</summary>
    public bool MbspResultIsFail => IsMbspComplete && !MbspResultIsPass;

    public bool IsAddMbspPage
    {
        get => _isAddMbspPage;
        private set
        {
            if (SetProperty(ref _isAddMbspPage, value))
                OnPropertyChanged(nameof(IsMbspPage));
        }
    }

    public bool IsMbspPage => !IsAddMbspPage;

    public IReadOnlyList<string> MbspPeriods { get; private set; } = [];

    public string SelectedMbspPeriod
    {
        get => _selectedMbspPeriod;
        set
        {
            if (SetProperty(ref _selectedMbspPeriod, value))
            {
                _mbspCorrectCount = 0;
                _mbspHistory.Clear();
                _mbspHistoryPosition = -1;
                _mbspAnswerHistory.Clear();
                ReshuffleMbspQueue();
                if (_mbspRemainingQueue.Count > 0)
                {
                    var idx = _mbspRemainingQueue[0];
                    _mbspRemainingQueue.RemoveAt(0);
                    _mbspHistory.Add(idx);
                    _mbspHistoryPosition = 0;
                    _currentMbspIndex = idx;
                    CurrentMbspQuestion = _mbspQuestions[idx];
                }
                else
                {
                    CurrentMbspQuestion = null;
                }
                OnPropertyChanged(nameof(MbspScoreText));
                OnPropertyChanged(nameof(MbspCorrectWrongText));
                OnPropertyChanged(nameof(MbspQuestionProgressText));
                OnPropertyChanged(nameof(IsMbspComplete));
                OnPropertyChanged(nameof(MbspResultIsPass));
                OnPropertyChanged(nameof(MbspResultIsFail));
                OnPropertyChanged(nameof(CanGoToPreviousMbsp));
                OnPropertyChanged(nameof(CanGoToNextMbsp));
            }
        }
    }

    public string NewMbspQuestion
    {
        get => _newMbspQuestion;
        set => SetProperty(ref _newMbspQuestion, value);
    }

    public string NewMbspQuestionEnglish
    {
        get => _newMbspQuestionEnglish;
        set => SetProperty(ref _newMbspQuestionEnglish, value);
    }

    public string NewMbspChoiceA
    {
        get => _newMbspChoiceA;
        set
        {
            if (SetProperty(ref _newMbspChoiceA, value))
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsA));
        }
    }

    public string NewMbspChoiceAEnglish
    {
        get => _newMbspChoiceAEnglish;
        set => SetProperty(ref _newMbspChoiceAEnglish, value);
    }

    public string NewMbspChoiceB
    {
        get => _newMbspChoiceB;
        set
        {
            if (SetProperty(ref _newMbspChoiceB, value))
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsB));
        }
    }

    public string NewMbspChoiceBEnglish
    {
        get => _newMbspChoiceBEnglish;
        set => SetProperty(ref _newMbspChoiceBEnglish, value);
    }

    public string NewMbspChoiceC
    {
        get => _newMbspChoiceC;
        set
        {
            if (SetProperty(ref _newMbspChoiceC, value))
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsC));
        }
    }

    public string NewMbspChoiceCEnglish
    {
        get => _newMbspChoiceCEnglish;
        set => SetProperty(ref _newMbspChoiceCEnglish, value);
    }

    public string NewMbspCorrectChoice
    {
        get => _newMbspCorrectChoice;
        set
        {
            if (SetProperty(ref _newMbspCorrectChoice, value))
            {
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsA));
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsB));
                OnPropertyChanged(nameof(NewMbspCorrectChoiceIsC));
            }
        }
    }

    /// <summary>True when Choice A text is selected as the correct answer (for radio button binding)</summary>
    public bool NewMbspCorrectChoiceIsA
    {
        get => !string.IsNullOrWhiteSpace(NewMbspChoiceA) &&
               string.Equals(NewMbspCorrectChoice, NewMbspChoiceA, StringComparison.Ordinal);
        set { if (value) NewMbspCorrectChoice = NewMbspChoiceA; }
    }

    public bool NewMbspCorrectChoiceIsB
    {
        get => !string.IsNullOrWhiteSpace(NewMbspChoiceB) &&
               string.Equals(NewMbspCorrectChoice, NewMbspChoiceB, StringComparison.Ordinal);
        set { if (value) NewMbspCorrectChoice = NewMbspChoiceB; }
    }

    public bool NewMbspCorrectChoiceIsC
    {
        get => !string.IsNullOrWhiteSpace(NewMbspChoiceC) &&
               string.Equals(NewMbspCorrectChoice, NewMbspChoiceC, StringComparison.Ordinal);
        set { if (value) NewMbspCorrectChoice = NewMbspChoiceC; }
    }

    private string? GetChoiceResult(string choiceLabel)
    {
        if (!MbspAnswerRevealed && MbspSelectedChoice is null) return null;
        if (_currentMbspQuestion is null) return null;

        // Get the text for this choice label
        var choiceText = choiceLabel switch
        {
            "A" => _currentMbspQuestion.ChoiceA,
            "B" => _currentMbspQuestion.ChoiceB,
            "C" => _currentMbspQuestion.ChoiceC ?? string.Empty,
            _ => string.Empty,
        };

        bool isCorrect = string.Equals(choiceText, _currentMbspQuestion.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
        bool isSelected = string.Equals(MbspSelectedChoice, choiceText, StringComparison.OrdinalIgnoreCase);

        if (MbspAnswerRevealed)
            return isCorrect ? "correct" : null;

        if (isSelected)
            return isCorrect ? "correct" : "wrong";

        if (!isCorrect) return null;
        // Highlight correct answer when a wrong one was selected
        return "highlight";
    }

    private string GetChoiceText(string? choiceLabel) => choiceLabel switch
    {
        "A" => _currentMbspQuestion?.ChoiceA ?? string.Empty,
        "B" => _currentMbspQuestion?.ChoiceB ?? string.Empty,
        "C" => _currentMbspQuestion?.ChoiceC ?? string.Empty,
        _ => string.Empty,
    };

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
        ShowAddSpeakingCommand = new RelayCommand(ShowAddSpeakingPage);
        ShowEditSpeakingCommand = new RelayCommand(ShowEditSpeaking);
        SaveNewSpeakingCommand = new RelayCommand(SaveNewSpeaking);
        CancelAddSpeakingCommand = new RelayCommand(CancelAddSpeaking);
        NextSpeakingCommand = new RelayCommand(SelectNextSpeakingEntry);
        PreviousSpeakingCommand = new RelayCommand(SelectPreviousSpeakingEntry);
        StartSpeakingTimerCommand = new AsyncRelayCommand(StartSpeakingTimerAndPlayAsync);
        PlayCommand = new RelayCommand(Play);
        PauseCommand = new RelayCommand(Pause);
        PreviousCardCommand = new RelayCommand(SelectPreviousFlashcard);
        NextCardCommand = new RelayCommand(SelectNextFlashcard);
        PlayDanishWordCommand = new AsyncRelayCommand(PlayCurrentDanishWord);
        PlayDanishExampleCommand = new AsyncRelayCommand(PlayCurrentDanishExample);
        PlaySpeakingTopicCommand = new AsyncRelayCommand(PlaySpeakingTopicAsync);
        MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
        SelectSearchResultCommand = new RelayCommand<FlashcardEntry?>(SelectSearchResult);
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        SelectTabCommand = new RelayCommand<string>(tab => { if (int.TryParse(tab, out var idx)) SelectedTabIndex = idx; });

        // MBSP
        NextMbspCommand = new RelayCommand(SelectNextMbspQuestion);
        PreviousMbspCommand = new RelayCommand(SelectPreviousMbspQuestion);
        SelectMbspChoiceCommand = new RelayCommand<string>(SelectMbspChoice);
        RevealMbspAnswerCommand = new RelayCommand(() => MbspAnswerRevealed = true);
        ShowAddMbspCommand = new RelayCommand(ShowAddMbspPage);
        ShowEditMbspCommand = new RelayCommand(ShowEditMbsp);
        SaveMbspCommand = new RelayCommand(SaveMbsp);
        CancelMbspCommand = new RelayCommand(CancelAddMbsp);
        SelectMbspCorrectChoiceCommand = new RelayCommand<string>(label =>
        {
            NewMbspCorrectChoice = GetChoiceTextForNew(label);
        });

        _rotationTimer = new DispatcherTimer
        {
            Interval = RotationInterval,
        };
        _rotationTimer.Tick += OnRotationTimerTick;

        MbspPeriods = new[] { "All" }
            .Concat(_mbspQuestions
                .Select(q => q.Period)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!)
                .Distinct()
                .OrderByDescending(p => p))
            .ToList();

        SelectNextFlashcard();
        UpdateRotationState();
        SelectFirstWritingEntry();
        SelectFirstSpeakingEntry();
        SelectFirstMbspQuestion();
    }

    public void Dispose()
    {
        _rotationTimer.Stop();
        _rotationTimer.Tick -= OnRotationTimerTick;
        _speakingTimer?.Stop();
        _speakingWordTimer?.Stop();
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
        var type = NewType.Trim();
        var conjugation = string.IsNullOrWhiteSpace(NewConjugation) ? null : NewConjugation.Trim();
        var exampleDanish = string.IsNullOrWhiteSpace(NewExampleDanish) ? null : NewExampleDanish.Trim();
        var exampleEnglish = string.IsNullOrWhiteSpace(NewExampleEnglish) ? null : NewExampleEnglish.Trim();
        var contextualTip = string.IsNullOrWhiteSpace(NewContextualTip) ? null : NewContextualTip.Trim();

        if (string.IsNullOrWhiteSpace(danish) || string.IsNullOrWhiteSpace(english))
        {
            ValidationMessage = "Danish and English are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            ValidationMessage = "Type is required (e.g. v., n., adj., adv., conj., prep.).";
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
        NewSpeakingTopic = string.Empty;
        NewSpeakingTopicTitle = string.Empty;
        NewSpeakingNotes = string.Empty;
        NewSpeakingNotesTitle = string.Empty;
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

    private IReadOnlyList<FlashcardEntry> GetFilteredFlashcards()
    {
        if (string.IsNullOrEmpty(_selectedTypeFilter) || _selectedTypeFilter == "All")
            return _flashcards;
        return _flashcards
            .Where(f => !string.IsNullOrWhiteSpace(f.Type) &&
                        f.Type.Split('/').Select(t => t.Trim()).Any(t => t == _selectedTypeFilter))
            .ToList();
    }

    private void SelectNextFlashcard()
    {
        var pool = GetFilteredFlashcards();
        if (pool.Count == 0)
        {
            CurrentFlashcard = null;
            return;
        }

        // Store the current flashcard in the history before moving to the next one
        if (CurrentFlashcard is not null)
        {
            _navigationHistory.Push(CurrentFlashcard);
        }

        if (pool.Count == 1)
        {
            CurrentFlashcard = pool[0];
            return;
        }

        FlashcardEntry nextFlashcard;

        do
        {
            nextFlashcard = pool[_random.Next(pool.Count)];
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

    // ─── Speaking Timer ─────────────────────────────────────────────────────────

    private async Task StartSpeakingTimerAndPlayAsync()
    {
        // Reset to 2 minutes and start counting down
        _speakingTimerSeconds = 120;
        OnPropertyChanged(nameof(SpeakingTimerDisplay));

        _speakingTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _speakingTimer.Tick -= OnSpeakingTimerTick;
        _speakingTimer.Tick += OnSpeakingTimerTick;
        _speakingTimer.Start();
        IsSpeakingTimerRunning = true;

        // Start word highlighting
        StartWordHighlight();

        // Also read the topic text aloud
        await PlaySpeakingTopicAsync();
    }

    private void OnSpeakingTimerTick(object? sender, EventArgs e)
    {
        if (_speakingTimerSeconds > 0)
        {
            _speakingTimerSeconds--;
            OnPropertyChanged(nameof(SpeakingTimerDisplay));
        }

        if (_speakingTimerSeconds == 0)
        {
            _speakingTimer?.Stop();
            IsSpeakingTimerRunning = false;
        }
    }

    private void ResetSpeakingTimer()
    {
        _speakingTimer?.Stop();
        IsSpeakingTimerRunning = false;
        _speakingTimerSeconds = 120;
        OnPropertyChanged(nameof(SpeakingTimerDisplay));
        ResetWordHighlight();
    }

    private void StartWordHighlight()
    {
        _speakingWords = SplitIntoWords(CurrentSpeakingTopic);
        _speakingTotalWords = _speakingWords.Count;
        _currentSpeakingWordIndex = 0;
        OnPropertyChanged(nameof(CurrentSpeakingTopicSegments));

        _speakingWordTimer ??= new DispatcherTimer();
        _speakingWordTimer.Tick -= OnSpeakingWordTimerTick;
        _speakingWordTimer.Tick += OnSpeakingWordTimerTick;
        // 600 ms lead-in before the first word is highlighted
        _speakingWordTimer.Interval = TimeSpan.FromMilliseconds(600) + WordDelay(_speakingWords, 0);
        _speakingWordTimer.Start();
    }

    private void OnSpeakingWordTimerTick(object? sender, EventArgs e)
    {
        // Stop, advance, re-start with the delay for the newly highlighted word
        _speakingWordTimer!.Stop();
        _currentSpeakingWordIndex++;

        if (_currentSpeakingWordIndex >= _speakingTotalWords)
        {
            _currentSpeakingWordIndex = -1;
            OnPropertyChanged(nameof(CurrentSpeakingTopicSegments));
            OnPropertyChanged(nameof(SpeakingWordProgress));
            return;
        }

        OnPropertyChanged(nameof(CurrentSpeakingTopicSegments));
        OnPropertyChanged(nameof(SpeakingWordProgress));
        _speakingWordTimer.Interval = WordDelay(_speakingWords, _currentSpeakingWordIndex);
        _speakingWordTimer.Start();
    }

    private void ResetWordHighlight()
    {
        _speakingWordTimer?.Stop();
        _currentSpeakingWordIndex = -1;
        _speakingTotalWords = 0;
        _speakingWords.Clear();
        OnPropertyChanged(nameof(CurrentSpeakingTopicSegments));
        OnPropertyChanged(nameof(SpeakingWordProgress));
    }

    /// <summary>
    /// Calculates the display duration for the word at <paramref name="index"/>.
    /// Base rate: ~75 ms per character, minimum 210 ms.
    /// Extra pause: +375 ms after sentence-ending punctuation (. ! ?),
    ///              +185 ms after mid-sentence pauses (, ;).
    /// </summary>
    private static TimeSpan WordDelay(List<string> words, int index)
    {
        if (index < 0 || index >= words.Count)
            return TimeSpan.FromMilliseconds(375);

        var word = words[index];

        // Determine punctuation bonus before stripping
        int punctuationBonus = 0;
        if (word.Length > 0)
        {
            char last = word[^1];
            if (last == '.' || last == '!' || last == '?')
                punctuationBonus = 375;
            else if (last == ',' || last == ';')
                punctuationBonus = 185;
        }

        // Strip trailing punctuation for character count
        var stripped = word.TrimEnd('.', ',', '!', '?', ';', ':');
        int ms = Math.Max(210, stripped.Length * 75) + punctuationBonus;
        return TimeSpan.FromMilliseconds(ms);
    }

    /// <summary>
    /// Splits text into a flat list of word tokens (whitespace/punctuation excluded).
    /// </summary>
    private static List<string> SplitIntoWords(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                  .ToList();

    private static IReadOnlyList<WritingSegment> BuildWordHighlightSegments(string text, int currentWordIndex)
    {
        var segments = new List<WritingSegment>();
        if (string.IsNullOrEmpty(text)) return segments;

        int wordIdx = 0;
        int i = 0;

        while (i < text.Length)
        {
            // Collect whitespace run
            int wsStart = i;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i > wsStart)
                segments.Add(new WritingSegment { Text = text[wsStart..i] });

            // Collect word token
            int wStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            if (i > wStart)
            {
                segments.Add(new WritingSegment
                {
                    Text = text[wStart..i],
                    IsHighlighted = currentWordIndex >= 0 && wordIdx == currentWordIndex
                });
                wordIdx++;
            }
        }

        return segments;
    }

    // ─── Speaking Navigation ────────────────────────────────────────────────────

    private void SelectFirstSpeakingEntry()
    {
        if (_speakingEntries.Count > 0)
            CurrentSpeakingEntry = _speakingEntries[0];
    }

    private void SelectNextSpeakingEntry()
    {
        if (_speakingEntries.Count == 0) { CurrentSpeakingEntry = null; return; }
        if (_speakingEntries.Count == 1) { CurrentSpeakingEntry = _speakingEntries[0]; return; }

        var idx = -1;
        for (int i = 0; i < _speakingEntries.Count; i++)
            if (ReferenceEquals(_speakingEntries[i], CurrentSpeakingEntry)) { idx = i; break; }

        CurrentSpeakingEntry = _speakingEntries[idx >= _speakingEntries.Count - 1 ? 0 : idx + 1];
    }

    private void SelectPreviousSpeakingEntry()
    {
        if (_speakingEntries.Count == 0) { CurrentSpeakingEntry = null; return; }

        var idx = -1;
        for (int i = 0; i < _speakingEntries.Count; i++)
            if (ReferenceEquals(_speakingEntries[i], CurrentSpeakingEntry)) { idx = i; break; }

        CurrentSpeakingEntry = idx <= 0
            ? _speakingEntries[_speakingEntries.Count - 1]
            : _speakingEntries[idx - 1];
    }

    private void ShowAddSpeakingPage()
    {
        ResetForm();
        IsAddSpeakingPage = true;
        IsEditMode = false;
    }

    private void ShowEditSpeaking()
    {
        if (CurrentSpeakingEntry is null) return;
        NewSpeakingTopicTitle = CurrentSpeakingEntry.TopicTitle;
        NewSpeakingTopic = CurrentSpeakingEntry.Topic;
        NewSpeakingNotesTitle = CurrentSpeakingEntry.NotesTitle;
        NewSpeakingNotes = CurrentSpeakingEntry.Notes;
        IsEditMode = true;
        IsAddSpeakingPage = true;
    }

    private void SaveNewSpeaking()
    {
        var topicTitle = NewSpeakingTopicTitle.Trim();
        var topic = NewSpeakingTopic.Trim();
        var notesTitle = NewSpeakingNotesTitle.Trim();
        var notes = NewSpeakingNotes.Trim();

        if (string.IsNullOrWhiteSpace(topic))
        {
            ValidationMessage = "Topic text is required.";
            return;
        }

        var entry = new SpeakingEntry
        {
            TopicTitle = topicTitle,
            Topic = topic,
            NotesTitle = notesTitle,
            Notes = notes,
        };

        if (IsEditMode)
        {
            var original = CurrentSpeakingEntry?.Topic ?? string.Empty;
            if (!SpeakingDataSource.TryUpdateEntry(original, entry))
            {
                ValidationMessage = "Unable to update: conflicts with an existing record.";
                return;
            }
            CurrentSpeakingEntry = entry;
            IsEditMode = false;
        }
        else
        {
            if (!SpeakingDataSource.TryAddEntry(entry))
            {
                ValidationMessage = $"A record with that topic already exists.";
                return;
            }
            CurrentSpeakingEntry = entry;
        }

        ResetForm();
        IsAddSpeakingPage = false;
    }

    private void CancelAddSpeaking()
    {
        ResetForm();
        IsAddSpeakingPage = false;
        IsEditMode = false;
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

    private async Task PlaySpeakingTopicAsync()
    {
        var text = CurrentSpeakingTopic;
        if (string.IsNullOrWhiteSpace(text) ||
            text == "No speaking entries available") return;
        try
        {
            foreach (var chunk in SplitIntoTtsChunks(text))
            {
                await _audioService.PlayDanishPronunciation(chunk);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ViewModel] PlaySpeakingTopicAsync: Error - {ex.Message}");
        }
    }

    /// <summary>
    /// Splits text into chunks of at most 180 characters, breaking on sentence
    /// boundaries (. ! ?) where possible, then on commas/spaces.
    /// </summary>
    private static IEnumerable<string> SplitIntoTtsChunks(string text, int maxLen = 180)
    {
        // Normalise whitespace
        text = text.Trim();
        while (text.Length > 0)
        {
            if (text.Length <= maxLen)
            {
                yield return text;
                yield break;
            }

            // Try to break on a sentence-ending punctuation within the limit
            int breakAt = -1;
            for (int i = maxLen; i >= maxLen / 2; i--)
            {
                char c = text[i - 1];
                if (c == '.' || c == '!' || c == '?')
                {
                    breakAt = i;
                    break;
                }
            }

            // Fall back to comma or space
            if (breakAt < 0)
            {
                for (int i = maxLen; i >= maxLen / 2; i--)
                {
                    char c = text[i - 1];
                    if (c == ',' || c == ' ')
                    {
                        breakAt = i;
                        break;
                    }
                }
            }

            // Hard cut if nothing suitable found
            if (breakAt < 0) breakAt = maxLen;

            yield return text[..breakAt].Trim();
            text = text[breakAt..].Trim();
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

    // ─── MBSP Methods ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds and shuffles a queue of all MBSP question indices (for the active period filter)
    /// using Fisher-Yates. Already-seen questions are excluded so we don't repeat them until
    /// the whole pool has been exhausted.
    /// </summary>
    private void ReshuffleMbspQueue()
    {
        var allIndices = (_selectedMbspPeriod == "All")
            ? Enumerable.Range(0, _mbspQuestions.Count).ToList()
            : _mbspQuestions
                .Select((q, i) => (q, i))
                .Where(x => string.Equals(x.q.Period, _selectedMbspPeriod, StringComparison.Ordinal))
                .Select(x => x.i)
                .ToList();

        // Exclude questions already in history so they aren't immediately repeated.
        var historySet = new HashSet<int>(_mbspHistory);
        var pool = allIndices.Where(i => !historySet.Contains(i)).ToList();

        // If every question has been shown, refill from the full set (new round).
        if (pool.Count == 0)
            pool = allIndices;

        // Fisher-Yates shuffle
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        _mbspRemainingQueue = pool;
    }

    private void SelectFirstMbspQuestion()
    {
        if (_mbspQuestions.Count == 0) return;
        ReshuffleMbspQueue();
        if (_mbspRemainingQueue.Count == 0) return;
        var idx = _mbspRemainingQueue[0];
        _mbspRemainingQueue.RemoveAt(0);
        _mbspHistory.Add(idx);
        _mbspHistoryPosition = 0;
        _currentMbspIndex = idx;
        CurrentMbspQuestion = _mbspQuestions[idx];
        OnPropertyChanged(nameof(MbspQuestionProgressText));
        OnPropertyChanged(nameof(CanGoToPreviousMbsp));
        OnPropertyChanged(nameof(CanGoToNextMbsp));
    }

    private void SelectNextMbspQuestion()
    {
        if (_mbspQuestions.Count == 0) return;
        if (IsMbspComplete) return; // test finished — Next is disabled

        if (_mbspHistoryPosition < _mbspHistory.Count - 1)
        {
            // Navigate forward within existing history.
            _mbspHistoryPosition++;
            NavigateToMbspHistoryPosition();
            return;
        }

        // At the end of history — pick the next unseen question.
        if (_mbspRemainingQueue.Count == 0)
            ReshuffleMbspQueue();

        if (_mbspRemainingQueue.Count == 0) return;

        var idx = _mbspRemainingQueue[0];
        _mbspRemainingQueue.RemoveAt(0);
        _mbspHistory.Add(idx);
        _mbspHistoryPosition = _mbspHistory.Count - 1;
        _currentMbspIndex = idx;
        CurrentMbspQuestion = _mbspQuestions[idx];
        OnPropertyChanged(nameof(MbspQuestionProgressText));
        OnPropertyChanged(nameof(CanGoToPreviousMbsp));
        OnPropertyChanged(nameof(CanGoToNextMbsp));
        // New question — no answer to restore yet.
    }

    private void SelectPreviousMbspQuestion()
    {
        if (_mbspHistoryPosition <= 0) return;
        _mbspHistoryPosition--;
        NavigateToMbspHistoryPosition();
    }

    /// <summary>
    /// Moves to the question at the current history position and restores its answer state.
    /// </summary>
    private void NavigateToMbspHistoryPosition()
    {
        _currentMbspIndex = _mbspHistory[_mbspHistoryPosition];
        // Setting CurrentMbspQuestion resets MbspSelectedChoice and MbspAnswerRevealed.
        CurrentMbspQuestion = _mbspQuestions[_currentMbspIndex];

        // Restore the previously saved answer (if any) without changing the score.
        if (_mbspAnswerHistory.TryGetValue(_currentMbspIndex, out var savedAnswer))
        {
            _mbspSelectedChoice = savedAnswer;
            OnPropertyChanged(nameof(MbspSelectedChoice));
            OnPropertyChanged(nameof(MbspChoiceASelected));
            OnPropertyChanged(nameof(MbspChoiceBSelected));
            OnPropertyChanged(nameof(MbspChoiceCSelected));
            OnPropertyChanged(nameof(MbspChoiceAResult));
            OnPropertyChanged(nameof(MbspChoiceBResult));
            OnPropertyChanged(nameof(MbspChoiceCResult));
            OnPropertyChanged(nameof(MbspAnswerFeedback));
            OnPropertyChanged(nameof(MbspHasFeedback));
        }
        OnPropertyChanged(nameof(MbspQuestionProgressText));
        OnPropertyChanged(nameof(CanGoToPreviousMbsp));
        OnPropertyChanged(nameof(CanGoToNextMbsp));
    }

    private void SelectMbspChoice(string? choiceLabel)
    {
        if (choiceLabel is null || MbspSelectedChoice is not null) return;
        // Store the actual text of the chosen option
        var text = GetChoiceText(choiceLabel);
        if (!string.IsNullOrEmpty(text))
        {
            MbspSelectedChoice = text;

            // Persist answer and update score only on first answer for this question.
            if (!_mbspAnswerHistory.ContainsKey(_currentMbspIndex))
            {
                _mbspAnswerHistory[_currentMbspIndex] = text;

                bool isCorrect = string.Equals(text, _currentMbspQuestion?.CorrectAnswer,
                    StringComparison.OrdinalIgnoreCase);
                if (isCorrect) _mbspCorrectCount++;
                OnPropertyChanged(nameof(MbspScoreText));
                OnPropertyChanged(nameof(MbspCorrectWrongText));
                OnPropertyChanged(nameof(IsMbspComplete));
                OnPropertyChanged(nameof(MbspResultIsPass));
                OnPropertyChanged(nameof(MbspResultIsFail));
                _ = isCorrect
                    ? _audioService.PlayCorrectSoundAsync()
                    : _audioService.PlayWrongSoundAsync();
            }
        }
    }

    private string GetChoiceTextForNew(string? label) => label switch
    {
        "A" => NewMbspChoiceA,
        "B" => NewMbspChoiceB,
        "C" => NewMbspChoiceC,
        _ => string.Empty,
    };

    public string NewMbspPeriod
    {
        get => _newMbspPeriod;
        set => SetProperty(ref _newMbspPeriod, value);
    }

    private void ShowAddMbspPage()
    {
        ResetMbspForm();
        IsAddMbspPage = true;
        IsEditMode = false;
    }

    private void ShowEditMbsp()
    {
        if (_currentMbspQuestion is null) return;
        NewMbspQuestion = _currentMbspQuestion.Question;
        NewMbspQuestionEnglish = _currentMbspQuestion.QuestionEnglish ?? string.Empty;
        NewMbspChoiceA = _currentMbspQuestion.ChoiceA;
        NewMbspChoiceAEnglish = _currentMbspQuestion.ChoiceAEnglish ?? string.Empty;
        NewMbspChoiceB = _currentMbspQuestion.ChoiceB;
        NewMbspChoiceBEnglish = _currentMbspQuestion.ChoiceBEnglish ?? string.Empty;
        NewMbspChoiceC = _currentMbspQuestion.ChoiceC ?? string.Empty;
        NewMbspChoiceCEnglish = _currentMbspQuestion.ChoiceCEnglish ?? string.Empty;
        NewMbspCorrectChoice = _currentMbspQuestion.CorrectAnswer;
        NewMbspPeriod = _currentMbspQuestion.Period ?? string.Empty;
        IsEditMode = true;
        IsAddMbspPage = true;
    }

    private void SaveMbsp()
    {
        var question = NewMbspQuestion.Trim();
        var questionEnglish = string.IsNullOrWhiteSpace(NewMbspQuestionEnglish) ? null : NewMbspQuestionEnglish.Trim();
        var choiceA = NewMbspChoiceA.Trim();
        var choiceAEnglish = string.IsNullOrWhiteSpace(NewMbspChoiceAEnglish) ? null : NewMbspChoiceAEnglish.Trim();
        var choiceB = NewMbspChoiceB.Trim();
        var choiceBEnglish = string.IsNullOrWhiteSpace(NewMbspChoiceBEnglish) ? null : NewMbspChoiceBEnglish.Trim();
        var choiceC = string.IsNullOrWhiteSpace(NewMbspChoiceC) ? null : NewMbspChoiceC.Trim();
        var choiceCEnglish = string.IsNullOrWhiteSpace(NewMbspChoiceCEnglish) ? null : NewMbspChoiceCEnglish.Trim();
        var correctAnswer = NewMbspCorrectChoice.Trim();
        var period = string.IsNullOrWhiteSpace(NewMbspPeriod) ? null : NewMbspPeriod.Trim();

        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(choiceA) ||
            string.IsNullOrWhiteSpace(choiceB))
        {
            ValidationMessage = "Question, Choice A and Choice B are required.";
            return;
        }

        // CorrectAnswer must match one of the choices
        bool matchesA = string.Equals(correctAnswer, choiceA, StringComparison.Ordinal);
        bool matchesB = string.Equals(correctAnswer, choiceB, StringComparison.Ordinal);
        bool matchesC = choiceC is not null && string.Equals(correctAnswer, choiceC, StringComparison.Ordinal);
        if (!matchesA && !matchesB && !matchesC)
        {
            ValidationMessage = "Please select the correct answer using the radio buttons.";
            return;
        }

        var entry = new MbspQuestion
        {
            Question = question,
            QuestionEnglish = questionEnglish,
            ChoiceA = choiceA,
            ChoiceAEnglish = choiceAEnglish,
            ChoiceB = choiceB,
            ChoiceBEnglish = choiceBEnglish,
            ChoiceC = choiceC,
            ChoiceCEnglish = choiceCEnglish,
            CorrectAnswer = correctAnswer,
            Period = period,
        };

        if (IsEditMode)
        {
            var original = _currentMbspQuestion?.Question+_currentMbspQuestion?.Period ?? string.Empty;
            if (!MbspDataSource.TryUpdateQuestion(original, entry))
            {
                ValidationMessage = "Unable to update: conflicts with an existing record.";
                return;
            }
            CurrentMbspQuestion = entry;
            IsEditMode = false;
        }
        else
        {
            if (!MbspDataSource.TryAddQuestion(entry))
            {
                ValidationMessage = "A question with that text already exists.";
                return;
            }
            _currentMbspIndex = _mbspQuestions.Count - 1;
            CurrentMbspQuestion = entry;
        }

        ResetMbspForm();
        IsAddMbspPage = false;
    }

    private void CancelAddMbsp()
    {
        ResetMbspForm();
        IsAddMbspPage = false;
        IsEditMode = false;
    }

    private void ResetMbspForm()
    {
        NewMbspQuestion = string.Empty;
        NewMbspQuestionEnglish = string.Empty;
        NewMbspChoiceA = string.Empty;
        NewMbspChoiceAEnglish = string.Empty;
        NewMbspChoiceB = string.Empty;
        NewMbspChoiceBEnglish = string.Empty;
        NewMbspChoiceC = string.Empty;
        NewMbspChoiceCEnglish = string.Empty;
        NewMbspCorrectChoice = string.Empty;
        NewMbspPeriod = string.Empty;
        ValidationMessage = string.Empty;
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