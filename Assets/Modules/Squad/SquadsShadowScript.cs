using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KModkit;
using UnityEngine;

public class SquadsShadowScript : MonoBehaviour
{
    [SerializeField]
    private GameObject _warningText;
    [SerializeField]
    private KMSelectable[] _buttons;
    [SerializeField]
    private TextMesh[] _digits;

    private readonly int _id = ++_idc;
    private static int _idc;

    private static readonly Harmony _harm = new Harmony("KTANE-Mod-Squad's-Shadow");
    private static bool _hasHarmed, _passThrough;
    private static readonly Dictionary<object, object> _allLinks = new Dictionary<object, object>();
    private static MethodInfo _SLPASS;

    private int _chains;
    private bool _isSolved;
    private int _submission;
    private float _lastPress;
    private bool _running;
    private static readonly Dictionary<string, int> _serials = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> _serialSubs = new Dictionary<string, int>();
    private string _sn;

    private void Start()
    {
#if UNITY_EDITOR
        Log("Looks like you're in the editor. This module won't work here.");
        _sn = "";
        return;
#endif
        _sn = GetComponent<KMBombInfo>().GetSerialNumber();
        if(_serials.ContainsKey(_sn))
        {
            _chains = _serials[_sn];
            _submission = _serialSubs[_sn];
            Log("There are multiple Squad's Shadows on this bomb, so all of their solutions are identical.");
            Warn(false);
            GetComponent<KMBombModule>().OnActivate += Activate;
            Press(-1);
            return;
        }
        HookHarmony();
        Warn(false);
        GetComponent<KMBombModule>().OnActivate += Activate;

        string[] ignored = GetComponent<KMBossModule>().GetIgnoredModules("Squad's Shadow", new string[] { "Squad's Shadow" });

        List<Module> allModules = new List<Module>();
        Type needyType = ReflectionHelper.FindTypeInGame("NeedyComponent");
        Type widgetType = ReflectionHelper.FindTypeInGame("WidgetComponent");
        Type timerType = ReflectionHelper.FindTypeInGame("TimerComponent");
        foreach(MonoBehaviour o in transform.root.gameObject.GetComponentsInChildren(ReflectionHelper.FindTypeInGame("BombComponent")).Cast<MonoBehaviour>())
        {
            KMBombModule m;
            if(m = o.GetComponent<KMBombModule>())
            {
                allModules.Add(new Module(o, ignored.Contains(m.ModuleDisplayName)));
            }
            else
            {
                if(o.GetComponent(widgetType) || o.GetComponent(timerType) || o.GetComponent(needyType))
                    continue;
                allModules.Add(new Module(o));
            }
        }
        DebugLog("Located {0} total modules.", allModules.Count);
        Module[] warnMods = allModules.Where(m => m.Ignored).ToArray();
        allModules.RemoveAll(m => m.Ignored);
        DebugLog("{0} of them are not ignored.", allModules.Count);
        int n = allModules.Count(m => m == null);
        if(n != 0)
        {
            DebugLog("{0} of them were null. Removing them.", n);
            allModules.RemoveAll(m => m == null);
        }

        foreach(Module m in warnMods)
            HookWarn(m);
        HookUnwarn(allModules);

        Type slType = ReflectionHelper.FindTypeInGame("StatusLight");
        List<Component> allSLs = allModules.Select(m => m.Obj.GetComponentInChildren(slType)).ToList();

        n = allSLs.Count(m => m == null);
        if(n != 0)
        {
            DebugLog("{0} status lights were null. Removing them.", n);
            //Log(allModules.Where(m => m.Obj.GetComponentInChildren(slType) == null).Select(m => m.Obj.GetType().AssemblyQualifiedName).Join(", "));
            allSLs.RemoveAll(m => m == null);
        }

        _submission = allSLs.Count;

        Component[] shuffleSLs = allSLs.ToArray().Shuffle();

        for(int i = 0; i < allSLs.Count; i++)
            _allLinks.Add(allSLs[i], shuffleSLs[i]);

        _chains = 0;
        while(allSLs.Count > 0)
        {
            object start = allSLs[0];
            object current = _allLinks[start];
            while(current != start)
            {
                allSLs.Remove(current as Component);
                current = _allLinks[current];
            }
            allSLs.Remove(current as Component);
            _chains++;
        }

        _serials.Add(_sn, _chains);
        _serialSubs.Add(_sn, _submission);

        if(_submission == 0)
            Log("Found no usable modules.");
        else if(_submission == 1)
            Log("Found only one usable module.");
        else
            Log("Found {0} usable modules.", _submission);

        if(_chains == 0)
            Log("There are 0 chains.");
        else if(_chains == 1)
            Log("There is only 1 chain.");
        else
            Log("There are {0} total chains.", _chains);
        if(_chains >= 100)
            Log("Since this number doesn't fit on the display, input it as {0} instead.", _chains % 100);

        Press(-1);
    }

    private void OnDestroy()
    {
        if(_serials.ContainsKey(_sn))
            _serials.Remove(_sn);
        if(_serialSubs.ContainsKey(_sn))
            _serialSubs.Remove(_sn);
    }

    private void Activate()
    {
        GetComponent<KMAudio>().PlaySoundAtTransform("Startup", transform);
        for(int i = 0; i < _buttons.Length; ++i)
        {
            int j = i;
            _buttons[i].OnInteract += () => { Press(j); return false; };
        }
    }

    private void Press(int ix)
    {
        if(ix >= 0 && ix < _buttons.Length)
            _buttons[ix].AddInteractionPunch(0.1f);
        if(_isSolved)
            return;
        switch(ix)
        {
            case 0:
                _submission += 10;
                break;
            case 1:
                _submission += 1;
                if(_submission % 10 == 0)
                    _submission -= 10;
                break;
            case 2:
                _submission -= 10;
                break;
            case 3:
                _submission -= 1;
                if(_submission % 10 == 9)
                    _submission += 10;
                break;
        }
        _submission %= 100;

        _digits[0].text = (_submission / 10).ToString();
        _digits[1].text = (_submission % 10).ToString();

        _lastPress = Time.time;
        if(!_running && ix >= 0 && ix < _buttons.Length)
            StartCoroutine(WaitForCheck());
    }

    private IEnumerator WaitForCheck()
    {
        _running = true;
        yield return new WaitUntil(() => Time.time - _lastPress >= 3f);
        if(_isSolved)
            yield break;
        if(_submission == _chains % 100)
        {
            Log("You submitted {0}, which is correct. Solved!", _submission);
            _isSolved = true;
            GetComponent<KMBombModule>().HandlePass();
            GetComponent<KMAudio>().PlaySoundAtTransform("Success", transform);
            Warn(false);
        }
        else
        {
            Log("You submitted {0}, which is incorrect. Strike!", _submission);
            GetComponent<KMBombModule>().HandleStrike();
        }
        _running = false;
    }

    private void HookHarmony()
    {
        if(_hasHarmed)
            return;
        DebugLog("Running Harmony hook...");
        _harm.Patch(_SLPASS = ReflectionHelper.FindTypeInGame("StatusLight").Method("SetPass"), new HarmonyMethod(GetType().Method("SLPrefix")));
        DebugLog("Succeeded!");
        _hasHarmed = true;
    }

    private void HookWarn(Module m)
    {
        m.Obj.GetComponent<KMBombModule>().OnPass += WarnSolve;
    }

    private void HookUnwarn(List<Module> mods)
    {
        FieldInfo field = ReflectionHelper.FindTypeInGame("BombComponent").GetField("OnPass", BindingFlags.Public | BindingFlags.Instance);
        Delegate del = Delegate.CreateDelegate(ReflectionHelper.FindTypeInGame("PassEvent"), this, GetType().Method("UnwarnSolve"));
        foreach(Module mod in mods)
            field.SetValue(mod.Obj, Delegate.Combine((Delegate)field.GetValue(mod.Obj), del));
    }

    private bool WarnSolve()
    {
        Warn(true);
        return false;
    }

    private bool UnwarnSolve(object o)
    {
        Warn(false);
        return false;
    }

    private void Warn(bool on)
    {
        if(_isSolved && on)
            return;
        _warningText.SetActive(on);
    }

    private void Log(string msg, params object[] args)
    {
        Debug.LogFormat("[Squad's Shadow #" + _id + "] " + msg, args);
    }

    private void DebugLog(string msg, params object[] args)
    {
        Debug.LogFormat("<Squad's Shadow #" + _id + "> " + msg, args);
    }

    private class Module
    {
        public Module(Component obj, bool ignored = false)
        {
            Obj = obj;
            Ignored = ignored;
        }

        public Component Obj;
        public bool Ignored;
    }

    private static bool SLPrefix(object __instance)
    {
        if(_passThrough)
            return true;

        if(!_allLinks.ContainsKey(__instance))
            return true;

        _passThrough = true;
        _SLPASS.Invoke(_allLinks[__instance], new object[0]);
        _passThrough = false;

        return false;
    }
}
