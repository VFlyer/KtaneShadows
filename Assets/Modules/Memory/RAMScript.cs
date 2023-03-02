using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using RNG = UnityEngine.Random;

public class RAMScript : MonoBehaviour
{
    [SerializeField]
    private TextMesh _maxText, _bytesText, _percentText, _dateText, _timeText, _greenBarText, _warningText, _RAMManageText, _ShutdownText, _ClearMemoryText;
    [SerializeField]
    private Transform _greenBar, _grayBar;
    [SerializeField]
    private KMBombInfo bombInfo;
    [SerializeField]
    private GameObject groupedRAM;
    [SerializeField]
    private KMSelectable clearBtn, shutdownButton;

    private readonly int _id = ++_idc;
    private static int _idc;

    private static List<Action<string>> _hooks = new List<Action<string>>();
    private Action<string> _hook;

    private static bool _harmed, _active;
    private float maxShutdownTime = 40f, timeRemaining = 0f, shutdownSpeed = 1f;

    private int _max, _bytes, nonIgnoredCount, nonIgnoredSolves, _bufferedBytes;

    enum ModeShadow
    {
        None = 0,
        OldMemShadow = 1,
        RAM = 2,
        ReimaginedShadow = 3
    }
    Dictionary<ModeShadow, string> descriptions = new Dictionary<ModeShadow, string> {
        { ModeShadow.None, "Basically The Simpleton, but the buttons are awkward. Using settings to disable both RAM and Memory's Shadow will cause this to occur." },
        { ModeShadow.OldMemShadow, "TLDR: The old Memory's Shadow. Basically before the takeover. Button presses will instantly add RAM, and the shutdown button basically solves the module but disables the functionality of clearing RAM." },
        { ModeShadow.RAM, "TLDR: Random Access Memory. There is a warning now in place now to clear memory before it's too late." },
        { ModeShadow.ReimaginedShadow, "TLDR: The reimagined take. Button presses will not immediately add RAM but instead are buffered to be added in. Clearing memory will only clear the RAM added in, not the ones being buffered. Shutting down has a penalty based on how many modules were solved." },
    };

    private bool _interactable, _isSolved, _shuttingDown;
    IEnumerable<string> ignoreList;
    MemorysShadowSettings memShadowSettings = new MemorysShadowSettings();
    ModeShadow memoryshadowMode;
    public class MemorysShadowSettings
    {
        public bool MemShadowMode = true;
        public bool RAMMode = true;
    }

    private void UpdateBar()
    {
        if (_shuttingDown)
        {
            float ratioTimer = timeRemaining / maxShutdownTime;
            ratioTimer = Mathf.Clamp(ratioTimer, 0f, 1f);
            _percentText.text = "";
            _greenBarText.color = new Color(1f,0.5f,0f);
            _greenBar.localScale = new Vector3(ratioTimer, _greenBar.localScale.y, _greenBar.localScale.z);
            _grayBar.localScale = new Vector3(1f - ratioTimer, _grayBar.localScale.y, _grayBar.localScale.z);
            return;
        }

        _bytesText.text = _bytes.ToString();
        float ratio = (float)_bytes / _max;
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        _percentText.text = Math.Round(100f * ratio, 2) + "%";
        _greenBarText.color = ratio > 0.8f ? Color.red : ratio > 0.5f ? Color.yellow : Color.green;
        _greenBar.localScale = new Vector3(ratio, _greenBar.localScale.y, _greenBar.localScale.z);
        _grayBar.localScale = new Vector3(1f - ratio, _grayBar.localScale.y, _grayBar.localScale.z);
    }
    private void Awake()
    {
        var allPossibleModes = new[] { ModeShadow.None, ModeShadow.OldMemShadow, ModeShadow.RAM, ModeShadow.ReimaginedShadow };
        try
        {
            ModConfig<MemorysShadowSettings> modSettings = new ModConfig<MemorysShadowSettings>("MemorysShadowSettings");
            memShadowSettings = modSettings.Settings;
            modSettings.Settings = memShadowSettings;

            memoryshadowMode = allPossibleModes[(memShadowSettings.RAMMode ? 2 : 0) + (memShadowSettings.MemShadowMode ? 1 : 0)];

        }
        catch
        {
            Debug.LogWarning("<Memory's Shadow Settings> Settings do not work as intended! Using default setting.");
            memoryshadowMode = ModeShadow.ReimaginedShadow;
        }
    }

    private void Start()
    {
        HookHarmony();
        var bossHandler = GetComponent<KMBossModule>();
        ignoreList = bossHandler.GetIgnoredModules(GetComponent<KMBombModule>(), new[] { "Memory's Shadow" });
        //nonIgnoredSolves = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
        nonIgnoredCount = bombInfo.GetSolvableModuleNames().Count(a => !ignoreList.Contains(a));
        Log("{0} ignored modules detected.", nonIgnoredCount);

        Log("Settings have altered to the following mode: {0}", memoryshadowMode.ToString());
        Log("Description: {0}", descriptions.ContainsKey(memoryshadowMode) ? descriptions[memoryshadowMode] : "<missing description>");

        _hooks.Add(_hook = AnyPress);

        int numMods = transform.root.GetComponentsInChildren<RAMScript>().Length;

        _max = RNG.Range(100, 251) + 10 * numMods;

        Log("You have {0} total bytes of RAM.", _max);

        _maxText.text = _max.ToString();
        _bytesText.text = _bytes.ToString();

        _interactable = true;

        clearBtn.OnInteract += ClearMemory;
        shutdownButton.OnInteract += Solve;

        StartCoroutine(DateTimeAnim());

        GetComponent<KMBombModule>().OnActivate += () => {
            _active = true;
            if (memoryshadowMode == ModeShadow.RAM)
                StartCoroutine(SimulateByteDataRAM());
            else if (memoryshadowMode == ModeShadow.ReimaginedShadow)
                StartCoroutine(SimulateByteDataReimagined());
        };
        GetComponent<KMBombInfo>().OnBombSolved += () => _active = false;

        _bytes = 0;
        UpdateBar();
        switch (memoryshadowMode)
        {
            case ModeShadow.None:
            case ModeShadow.OldMemShadow:
                _ShutdownText.text = "Solve Module";
                break;
            case ModeShadow.ReimaginedShadow:
                _ShutdownText.text = "Shutdown";
                break;
            default:
                _ShutdownText.text = "";
                break;
        }
    }
    IEnumerator SimulateByteDataReimagined()
    {
        while (!_shuttingDown)
        {
            yield return new WaitForSeconds(RNG.value * 10);
            if (_bufferedBytes > 0)
            {
                var amountToAdd = RNG.Range(1, Mathf.Min(11, _bufferedBytes));
                _bufferedBytes -= amountToAdd;
                _bytes += amountToAdd;
                UpdateBar();
                if (_bytes > _max)
                    yield return Strike();
            }
        }
    }
    IEnumerator SimulateByteDataRAM()
    {
        while (!_shuttingDown)
        {
            yield return new WaitForSeconds(RNG.value * 5);
            int used = RNG.Range(0, 10);
            _bytes += used;
            UpdateBar();
            if (_bytes * 5 >= _max * 4)
                GetComponent<KMAudio>().PlaySoundAtTransform("459992__florianreichelt__beep-short", transform);
            if (_bytes > _max)
                yield return Strike();
            nonIgnoredSolves = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
            _shuttingDown = nonIgnoredSolves * 5 >= nonIgnoredCount * 2;

        }
        Log("Enough non-ignored modules have been solved to activate the shutdown sequence.");
        GetComponent<KMAudio>().PlaySoundAtTransform("4beeps", transform);
        _bytesText.text = "-";
        _warningText.text = "Caution!\n\nA shutdown has been activated!\nIn the event of a power outage,\nensure all important files are saved.";
        _RAMManageText.text = "Emergency Shutdown";
        while (timeRemaining <= maxShutdownTime)
        {
            yield return null;
            timeRemaining += Time.deltaTime * maxShutdownTime;
            UpdateBar();
        }
        timeRemaining = maxShutdownTime;

        while (timeRemaining >= 0f)
        {
            yield return null;
            timeRemaining -= Time.deltaTime * shutdownSpeed;
            UpdateBar();
        }
        _ShutdownText.text = "Shutdown";
    }
    IEnumerator DelayClearRAM()
    {
        yield return new WaitForSeconds(0.5f);
        float ratio = (float)_bytes / _max;
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        if (ratio > 0.25f)
        {
            _bytes = memoryshadowMode == ModeShadow.ReimaginedShadow ? 0 : _bytes / 2;
            Log("Memory cleared.");
        }
        UpdateBar();
        _ClearMemoryText.text = "Clear Memory";
        _active = true;
    }


    private bool Solve()
    {
        switch (memoryshadowMode)
        {
            case ModeShadow.ReimaginedShadow:
                {

                    //_isSolved = true;
                    if (!_shuttingDown)
                    {
                        _shuttingDown = true;
                        Log("You pressed the shutdown button. Let's hope you don't use up too much memory before this module fully shuts down.");
                        StartCoroutine(DelaySolveDisableModule());
                    }
                }
                break;
            case ModeShadow.OldMemShadow:
                {
                    if (!_isSolved)
                    {
                        _isSolved = true;
                        GetComponent<KMBombModule>().HandlePass();
                        Log("You pressed the solve button. The module has been solved, but let's hope you don't use up too much memory...");
                    }
                }
                break;
            case ModeShadow.RAM:
                {
                    if (_shuttingDown && timeRemaining <= 0f)
                    {
                        _isSolved = true;
                        GetComponent<KMBombModule>().HandlePass();
                        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                        _active = false;
                        groupedRAM.SetActive(false);
                        Log("You pressed the shutdown button. The module has now been disarmed.");
                    }
                }
                break;
            default:
                {
                    _isSolved = true;
                    _active = false;
                    GetComponent<KMBombModule>().HandlePass();
                    Log("You pressed the solve button. The module has now been disarmed.");
                }
                break;
        }
        return false;
    }

    private IEnumerator DateTimeAnim()
    {
        while(_active || !_isSolved)
        {
            _dateText.text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            _timeText.text = DateTime.Now.ToString("HH:mm");
            yield return null;
        }
        _dateText.text = "";
        _timeText.text = "";
    }

    private bool ClearMemory()
    {
        if(_isSolved)
            return false;
        if (memoryshadowMode == ModeShadow.RAM || memoryshadowMode == ModeShadow.ReimaginedShadow)
        {
            if (!_shuttingDown && _active)
            {
                _active = false;
                _ClearMemoryText.text = "Clearing...";
                StartCoroutine(DelayClearRAM());
            }
            return false;
        }
        _bytes = 0;
        UpdateBar();
        Log("Memory reset.");

        return false;
    }

    private void HookHarmony()
    {
        if (Application.isEditor)
        {
            memoryshadowMode = memShadowSettings.RAMMode ? ModeShadow.RAM : ModeShadow.None;
            Log("This module is being ran in TestHarness. The actual functionality is altered here for the time being.");
            return;
        }
        if(_harmed)
            return;

        _harmed = true;

        Harmony harm = new Harmony("KTANE-Mod-Memory's-Shadow");

        harm.Patch(ReflectionHelper.FindTypeInGame("Selectable").Method("HandleInteract"), postfix: new HarmonyMethod(GetType().Method("Postfix")));
    }

    private void OnDestroy()
    {
        _hooks.Remove(_hook);
    }

    private void AnyPress(string button)
    {
        if(!_interactable || !_active || memoryshadowMode == ModeShadow.None || memoryshadowMode == ModeShadow.RAM)
            return;

        int rand = RNG.Range(0, 10);
        int used = 1;
        string plural = "";
        if(rand > 5)
        {
            used++;
            plural = "s";
        }
        if(used > 8)
            used++;
        if (memoryshadowMode == ModeShadow.OldMemShadow)
            _bytes += used;
        else
            _bufferedBytes += used;
        float ratio = Mathf.Clamp01((float)_bytes / _max);
        if (ratio >= 0.8f)
            GetComponent<KMAudio>().PlaySoundAtTransform("459992__florianreichelt__beep-short", transform);

        Log("The button \"{0}\" was pressed. That used {1} byte{2} of memory.", button, used, plural);
        UpdateBar();
        if (_bytes > _max)
            StartCoroutine(Strike());
    }

    private IEnumerator DelaySolveDisableModule()
    {
        nonIgnoredSolves = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
        _warningText.text = "Caution!\n\nA shutdown has been activated!\nSaving remaining files...\nFinalizing RAM usage...";
        _RAMManageText.text = "Shutdown Sequence Activated";
        var timerSpeed = 1f;
        var TBomb = GetComponentInParent<KMBomb>();
        if (TBomb != null)
        {
            var TScript = TBomb.GetComponent("Bomb");
            if (TScript != null)
            {
                timerSpeed = TScript.CallMethod<object>("GetTimer").CallMethod<float>("GetRate");
            }
            else
                Debug.Log("TScript = null");
        }
        else
            Debug.Log("TBomb = null");
        maxShutdownTime = 30f;

        if (nonIgnoredSolves * 5 < nonIgnoredCount * 1)
        { // Severely penalize the user if the non-ignored solves was less than 1/5 of all non-ignored by increaing the shutdown timer by 10% of the bomb's countdown timer.
            Log("The shutdown button was pressed with {0} / {1} non-ignored solved. A shutdown penalty has been issued for this module at {2}. (Penalty: 20% of remaining time, adjusted by timer speed, min 90 seconds)", nonIgnoredSolves, nonIgnoredCount, bombInfo.GetFormattedTime());
            maxShutdownTime = Mathf.Max(bombInfo.GetTime() / (timerSpeed <= 0 ? 1f : timerSpeed) / 5f, 90f);
        }
        else  if (nonIgnoredSolves * 5 < nonIgnoredCount * 2)
        { // Penalize the user if the non-ignored solves was less than 2/5 of all non-ignored by increaing the shutdown timer by 10% of the bomb's countdown timer.
            Log("The shutdown button was pressed with {0} / {1} non-ignored solved. A shutdown penalty has been issued for this module at {2}. (Penalty: 10% of remaining time, adjusted by timer speed, min 45 seconds)", nonIgnoredSolves, nonIgnoredCount, bombInfo.GetFormattedTime());
            maxShutdownTime = Mathf.Max(bombInfo.GetTime() / (timerSpeed <= 0 ? 1f : timerSpeed) / 10f, 45f);
        }
        else if (nonIgnoredSolves >= nonIgnoredCount)
        {
            maxShutdownTime = 8f;
            Log("The shutdown button was pressed with no unsolved non-ignored modules. The shutdown time will last for 8 seconds.");
        }
        else
            Log("The shutdown button was pressed with {0} / {1} non-ignored solved. The shutdown will last for 30 seconds.", nonIgnoredSolves, nonIgnoredCount);
        Log("Shutdown initiated for {0} seconds until disarm.", maxShutdownTime.ToString("0.00"));
        timeRemaining = 0f;
        GetComponent<KMAudio>().PlaySoundAtTransform("4beeps", transform);
        while (timeRemaining <= maxShutdownTime)
        {
            yield return null;
            timeRemaining += Time.deltaTime * maxShutdownTime;
            UpdateBar();
        }
        timeRemaining = maxShutdownTime;
        
        while (timeRemaining >= 0f)
        {
            yield return null;
            timeRemaining -= Time.deltaTime * shutdownSpeed;
            UpdateBar();
        }
        yield return null;
        _isSolved = true;
        GetComponent<KMBombModule>().HandlePass();
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
        _active = false;
        groupedRAM.SetActive(false);
    }


    private IEnumerator Strike()
    {
        if (memoryshadowMode != ModeShadow.OldMemShadow)
            Log("Too much RAM is being used up! A strike will incur for failing to account for this if the memory is not cleared.");
        else
            Log("Too much RAM is being used up! A strike will incur for failing to account for this, even if the memory is cleared.");
        _interactable = false;

        GetComponent<KMAudio>().PlaySoundAtTransform("4beeps", transform);

        yield return new WaitForSeconds(8f);
        if (_bytes > _max || memoryshadowMode == ModeShadow.OldMemShadow)
        {
            GetComponent<KMBombModule>().HandleStrike();
            _bytes = 0;
            _interactable = !_isSolved;
            UpdateBar();
            if (_shuttingDown || (_isSolved && memoryshadowMode == ModeShadow.OldMemShadow))
            {
                _active = false;
                Log("Overflowing the RAM was not a good idea after all. Expecially when a shutdown has been initiated.");
                timeRemaining = 0f;
            }
        }
    }

    private void LogDebug(string msg, params object[] args)
    {
        Debug.LogFormat("<Memory's Shadow #{0}> {1}", _id , string.Format(msg, args));
    }
    
    private void Log(string msg, params object[] args)
    {
        Debug.LogFormat("[Memory's Shadow #{0}] {1}", _id , string.Format(msg, args));
    }

    private static void Postfix(MonoBehaviour __instance, Array ___Children)
    {
        if(___Children == null || ___Children.Length == 0)
            foreach(Action<string> h in _hooks)
                h(__instance.gameObject.name);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "\"!{0} clear/c/press/p/push/clear memory\" [Presses the button that clears memory.] |\"!{0} shutdown\" [Activates shutdown sequence. WARNING: No bomb reward will be given if less than 2/5 of all non-ignored modules are solved!]";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        string[] acceptable = new string[] { "clear", "c", "press", "p", "push", "clear memory" };
        if(acceptable.Contains(command))
        {
            yield return null;
            clearBtn.OnInteract();
        }
        if(command == "shutdown")
        {
            yield return null;
            shutdownButton.OnInteract();
            yield return "solve";
            yield return "strike";
            nonIgnoredSolves = bombInfo.GetSolvedModuleNames().Count(a => !ignoreList.Contains(a));
            if ((nonIgnoredSolves * 5 < nonIgnoredCount * 2 && memoryshadowMode != ModeShadow.RAM) || memoryshadowMode == ModeShadow.None)
                yield return "nobombreward";
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        _interactable = false;
        if (_bytes + 3 >= _max)
            clearBtn.OnInteract();
        Log("Issuing force solve. Also accelerating shutdown timer speed.");
        if (!_shuttingDown)
            shutdownButton.OnInteract();
        shutdownSpeed = 100f;
        while (!_isSolved)
            yield return true;
    }
}
