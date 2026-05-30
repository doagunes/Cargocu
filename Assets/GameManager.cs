using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public struct Shift
{
    public int targetScore;
    public float timeLimit;
    public float speedMultiplier;
    public int packageCount;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Vardiya (Level) Ayarlar�")]
    public Shift[] shifts;
    private int currentShiftIndex = 0;
    private bool isLevelCompleted = false; // YEN�: Levelin defalarca bitmesini engellemek i�in

    float timeRemaining;
    bool gameStarted = false;
    bool gameOver = false;
    private bool isPaused = false;

    [Header("UI Panelleri")]
    [SerializeField] Text timerText;
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip levelCompleteSound;
    [SerializeField] AudioClip gameOverSound;

    public Text currentLevelText; // YEN�: Sol altta yazacak level yaz�s�

    [Header("Duraklatma ve Seviye UI")]
    public GameObject pausePanel;
    public GameObject levelsPanel;
    public Button[] levelButtons;

    [Header("Kargo �retim (Spawn) Ayarlar�")]
    public GameObject packagePrefab;
    public Transform[] spawnPoints;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentShiftIndex = PlayerPrefs.GetInt("SelectedLevel", 0);

        if (shifts.Length > 0 && currentShiftIndex < shifts.Length)
            timeRemaining = shifts[currentShiftIndex].timeLimit;
        else
            timeRemaining = 120f;

        // Sol alt level yaz�s�n� g�ncelle
        if (currentLevelText != null)
        {
            currentLevelText.text = "Level " + (currentShiftIndex + 1);
        }

        UpdateLevelButtons(); // Butonlar�n kilidini ayarlayan yard�mc� fonksiyon

        Time.timeScale = 0f;
        if (startPanel != null) startPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (levelsPanel != null) levelsPanel.SetActive(false);
    }

    void Update()
    {
        // ESC'ye bas�ld���nda, oyun oynan�yorsa ve level bitmemi�se men�y� a�/kapat
        if (Input.GetKeyDown(KeyCode.Escape) && gameStarted && !gameOver && !isLevelCompleted)
        {
            TogglePause();
        }

        if (!gameStarted || gameOver || isPaused || isLevelCompleted) return;

        timeRemaining -= Time.deltaTime;

        if (timerText != null)
        {
            int minutes = (int)(timeRemaining / 60);
            int seconds = (int)(timeRemaining % 60);
            timerText.text = minutes + ":" + seconds.ToString("00");
        }

        if (timeRemaining <= 0)
        {
            gameOver = true;
            timeRemaining = 0;

            if (gameOverSound != null)
            {
                AudioSource.PlayClipAtPoint(gameOverSound, Vector3.zero);
            }

            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }
    }

    public void StartGame()
    {
        gameStarted = true;
        Time.timeScale = 1f;
        if (startPanel != null) startPanel.SetActive(false);

        if (shifts.Length > 0 && currentShiftIndex < shifts.Length)
        {
            Driver driver = FindAnyObjectByType<Driver>();
            if (driver != null) driver.SetSpeedMultiplier(shifts[currentShiftIndex].speedMultiplier);

            SpawnPackages();
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void CheckShiftProgress(int currentScore)
    {
        // E�er level zaten bittiyse (men� a��ld�ysa) skoru tekrar kontrol etme
        if (isLevelCompleted) return;

        if (currentScore >= shifts[currentShiftIndex].targetScore)
        {
            LevelCompleted();
        }
    }

    // --- YEN�: LEVEL B�T�� KONTROL� ---
    private void LevelCompleted()
{
    if (isLevelCompleted) return;

    isLevelCompleted = true;

    if (audioSource != null && levelCompleteSound != null)
    {
        audioSource.PlayOneShot(levelCompleteSound);
    }

    StartCoroutine(OpenLevelsMenuAfterSound());
}
IEnumerator OpenLevelsMenuAfterSound()
{
    yield return new WaitForSeconds(0.7f);

    int nextLevel = currentShiftIndex + 1;
    int maxUnlocked = PlayerPrefs.GetInt("UnlockedLevel", 0);

    if (nextLevel > maxUnlocked && nextLevel < shifts.Length)
    {
        PlayerPrefs.SetInt("UnlockedLevel", nextLevel);
    }

    UpdateLevelButtons();

    isPaused = true;
    Time.timeScale = 0f;

    if (pausePanel != null) pausePanel.SetActive(false);
    if (levelsPanel != null) levelsPanel.SetActive(true);
}

    // --- MEN�, SEV�YE VE KAYIT KONTROL FONKS�YONLARI ---
    private void UpdateLevelButtons()
    {
        int unlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 0);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] != null)
            {
                levelButtons[i].interactable = (i <= unlockedLevel);
            }
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        if (pausePanel != null) pausePanel.SetActive(isPaused);

        if (!isPaused && levelsPanel != null) levelsPanel.SetActive(false);

        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void OpenLevelsMenu()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (levelsPanel != null) levelsPanel.SetActive(true);
    }

    public void LoadSpecificLevel(int levelIndex)
    {
        PlayerPrefs.SetInt("SelectedLevel", levelIndex);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // YEN�: �lerlemeyi s�f�rlama butonu i�in fonksiyon
    public void ResetAllProgress()
    {
        PlayerPrefs.SetInt("UnlockedLevel", 0);
        PlayerPrefs.SetInt("SelectedLevel", 0);
        UpdateLevelButtons(); // Butonlar� an�nda kilitle
    }

    // --- D�NAM�K KARGO OLU�TURMA S�STEM� ---
    private void SpawnPackages()
    {
        if (packagePrefab == null || spawnPoints.Length == 0) return;

        int amountToSpawn = shifts[currentShiftIndex].packageCount;
        if (amountToSpawn > spawnPoints.Length) amountToSpawn = spawnPoints.Length;

        List<Transform> availablePoints = new List<Transform>(spawnPoints);

        for (int i = 0; i < amountToSpawn; i++)
        {
            int randomIndex = Random.Range(0, availablePoints.Count);
            Instantiate(packagePrefab, availablePoints[randomIndex].position, Quaternion.identity);
            availablePoints.RemoveAt(randomIndex);
        }
    }
}