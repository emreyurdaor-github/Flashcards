using System;
using System.Collections.Generic;
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
    private static readonly TimeSpan RotationInterval = TimeSpan.FromSeconds(10);
    private readonly IReadOnlyList<FlashcardEntry> _flashcards = FlashcardDataSource.GetFlashcards();
    private readonly DispatcherTimer _rotationTimer;
    private readonly Random _random = new();
    private readonly AudioService _audioService = new();
    private readonly Stack<FlashcardEntry> _navigationHistory = new();
    private FlashcardEntry? _currentFlashcard;
    private bool _isAddRecordPage;
    private bool _isRotationPaused;
    private string _newDanish = string.Empty;
    private string _newEnglish = string.Empty;
    private string _newConjugation = string.Empty;
    private string _newExampleDanish = string.Empty;
    private string _newExampleEnglish = string.Empty;
    private string _newContextualTip = string.Empty;
    private string _validationMessage = string.Empty;

    public string DataSourceName { get; } = "Local in-solution data source";
    public IRelayCommand ShowAddRecordCommand { get; }
    public IRelayCommand SaveNewRecordCommand { get; }
    public IRelayCommand CancelAddRecordCommand { get; }
    public IRelayCommand ShowEditRecordCommand { get; }
    public IRelayCommand PlayCommand { get; }
    public IRelayCommand PauseCommand { get; }
    public IRelayCommand PreviousCardCommand { get; }
    public IRelayCommand NextCardCommand { get; }
    public IAsyncRelayCommand PlayDanishWordCommand { get; }
    public IAsyncRelayCommand PlayDanishExampleCommand { get; }

    public FlashcardEntry? CurrentFlashcard
    {
        get => _currentFlashcard;
        private set
        {
            if (SetProperty(ref _currentFlashcard, value))
            {
                OnPropertyChanged(nameof(CurrentDanish));
                OnPropertyChanged(nameof(CurrentEnglish));
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
            }
        }
    }

    public string CurrentDanish => CurrentFlashcard?.Danish ?? "No flashcards available";

    public string CurrentEnglish => CurrentFlashcard?.English ?? string.Empty;

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
        
        // Try to find any of the keywords in the text
        foreach (var keyword in keywords)
        {
            var result = FindAndHighlightWord(text, keyword);
            if (!string.IsNullOrEmpty(result.HighlightedWord))
                return result;
        }

        // If no keywords found, return the full text without highlighting
        // This ensures the text is still visible even if highlighting fails
        return new FormattedExampleText { FullText = text };
    }

    /// <summary>
    /// Extracts keywords from a word expression (e.g., "to destroy / to ruin / to break" -> ["destroy", "ruin", "break"])
    /// Also handles multi-word phrases like "at skabe" -> ["skabe"]
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
            
            // If it's a multi-word phrase, skip common prefixes like "to", "at", "de"
            if (words.Length > 1)
            {
                // Language-specific prefixes to skip in multi-word phrases
                var prefixesToSkip = new[] { "to", "at", "de", "for" };
                
                foreach (var singleWord in words)
                {
                    var lowerWord = singleWord.ToLowerInvariant();
                    
                    // Skip single-letter words and common prefixes
                    if (lowerWord.Length <= 1 || Array.Exists(prefixesToSkip, p => p == lowerWord))
                        continue;
                    
                    var rootWord = ExtractRootWord(singleWord);
                    
                    if (!string.IsNullOrEmpty(rootWord) && !keywords.Contains(rootWord, StringComparer.OrdinalIgnoreCase))
                        keywords.Add(rootWord);
                }
            }
            else
            {
                // Single word - process normally
                var singleWord = words[0];
                var wordToProcess = singleWord;
                
                // Remove common prefixes like "to ", "at ", etc. only if they have a suffix
                if (wordToProcess.StartsWith("to ", StringComparison.OrdinalIgnoreCase))
                    wordToProcess = wordToProcess.Substring(3).Trim();
                if (wordToProcess.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                    wordToProcess = wordToProcess.Substring(3).Trim();
                
                var rootWord = ExtractRootWord(wordToProcess);
                
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
    /// Priority: exact match > case-insensitive match > word form match
    /// </summary>
    private FormattedExampleText FindAndHighlightWord(string text, string keyword)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
            return new FormattedExampleText { FullText = text };

        var lowerText = text.ToLowerInvariant();
        var lowerKeyword = keyword.ToLowerInvariant();

        // First, try exact match (case-insensitive)
        var index = lowerText.IndexOf(lowerKeyword, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return ExtractHighlightedText(text, index, keyword.Length);
        }

        // Second, try to find word forms (e.g., "destroy" in "destroying")
        var variants = GenerateWordVariants(lowerKeyword);
        foreach (var variant in variants)
        {
            index = lowerText.IndexOf(variant, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return ExtractHighlightedText(text, index, variant.Length);
            }
        }

        // If still no match, return full text (don't hide it)
        return new FormattedExampleText { FullText = text };
    }

    /// <summary>
    /// Generates common word variants (e.g., "destroy" -> ["destroy", "destroys", "destroyed", "destroying", "destroyer"])
    /// </summary>
    private List<string> GenerateWordVariants(string word)
    {
        var variants = new List<string> { word };
        
        // Add common endings
        variants.Add(word + "ing");
        variants.Add(word + "ed");
        variants.Add(word + "s");
        variants.Add(word + "er");
        variants.Add(word + "est");
        
        // Add variants with common suffix replacements
        if (word.EndsWith("e"))
        {
            variants.Add(word.Substring(0, word.Length - 1) + "ing");
            variants.Add(word.Substring(0, word.Length - 1) + "ed");
        }
        
        // For words ending in consonant, double the consonant before adding suffix
        if (word.Length > 1 && !IsVowel(word[word.Length - 1]) && IsVowel(word[word.Length - 2]))
        {
            variants.Add(word + word[word.Length - 1] + "ing");
            variants.Add(word + word[word.Length - 1] + "ed");
        }

        return variants.OrderByDescending(v => v.Length).ToList(); // Longer matches first
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
    /// </summary>
    private void UpdateHighlightedExamples()
    {
        if (string.IsNullOrEmpty(CurrentExampleDanish) || string.IsNullOrEmpty(CurrentDanish))
            HighlightedExampleDanish = new FormattedExampleText { FullText = CurrentExampleDanish ?? string.Empty };
        else
            HighlightedExampleDanish = HighlightWord(CurrentExampleDanish, CurrentDanish);

        if (string.IsNullOrEmpty(CurrentExampleEnglish) || string.IsNullOrEmpty(CurrentEnglish))
            HighlightedExampleEnglish = new FormattedExampleText { FullText = CurrentExampleEnglish ?? string.Empty };
        else
            HighlightedExampleEnglish = HighlightWord(CurrentExampleEnglish, CurrentEnglish);
    }

    public bool HasFlashcards => CurrentFlashcard is not null;

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

    public MainWindowViewModel()
    {
        ShowAddRecordCommand = new RelayCommand(ShowAddRecordPage);
        ShowEditRecordCommand = new RelayCommand(ShowEditRecord);
        SaveNewRecordCommand = new RelayCommand(SaveNewRecord);
        CancelAddRecordCommand = new RelayCommand(CancelAddRecord);
        PlayCommand = new RelayCommand(Play);
        PauseCommand = new RelayCommand(Pause);
        PreviousCardCommand = new RelayCommand(SelectPreviousFlashcard);
        NextCardCommand = new RelayCommand(SelectNextFlashcard);
        PlayDanishWordCommand = new AsyncRelayCommand(PlayCurrentDanishWord);
        PlayDanishExampleCommand = new AsyncRelayCommand(PlayCurrentDanishExample);

        _rotationTimer = new DispatcherTimer
        {
            Interval = RotationInterval,
        };
        _rotationTimer.Tick += OnRotationTimerTick;

        SelectNextFlashcard();
        UpdateRotationState();
    }

    public void Dispose()
    {
        _rotationTimer.Stop();
        _rotationTimer.Tick -= OnRotationTimerTick;
        _audioService?.Dispose();
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

    private void ResetForm()
    {
        NewDanish = string.Empty;
        NewEnglish = string.Empty;
        NewConjugation = string.Empty;
        NewExampleDanish = string.Empty;
        NewExampleEnglish = string.Empty;
        NewContextualTip = string.Empty;
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

        System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayCurrentDanishWord: Playing '{CurrentFlashcard.Danish}'");
        await _audioService.PlayDanishPronunciation(CurrentFlashcard.Danish);
    }

    private async Task PlayCurrentDanishExample()
    {
        if (CurrentFlashcard is null || string.IsNullOrWhiteSpace(CurrentFlashcard.ExampleDanish))
        {
            System.Diagnostics.Debug.WriteLine("[ViewModel] PlayCurrentDanishExample: No example available");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ViewModel] PlayCurrentDanishExample: Playing '{CurrentFlashcard.ExampleDanish}'");
        await _audioService.PlayDanishPronunciation(CurrentFlashcard.ExampleDanish);
    }
}