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
    private static Dictionary<object, object> _SLToMod = new Dictionary<object, object>();
    private static MethodInfo _SLPASS;
    private static Type _bcType;
    private static MethodInfo _getDisplayName;

    private int _chains;
    private bool _isSolved, _first;
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
        GetComponent<KMBombModule>().OnActivate += Activate;
        _submission = 69;
        _chains = 47;
        Press(-1);
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

        _first = true;

        HookHarmony();
        Warn(false);
        GetComponent<KMBombModule>().OnActivate += Activate;

        string[] ignored = GetComponent<KMBossModule>().GetIgnoredModules("Squad's Shadow", new string[] { "Squad's Shadow" });

        List<Module> allModules = new List<Module>();
        Type needyType = ReflectionHelper.FindTypeInGame("NeedyComponent");
        Type widgetType = ReflectionHelper.FindTypeInGame("WidgetComponent");
        Type timerType = ReflectionHelper.FindTypeInGame("TimerComponent");
        _bcType = ReflectionHelper.FindTypeInGame("BombComponent");
        _getDisplayName = _bcType.Method("GetModuleDisplayName");
        foreach(MonoBehaviour o in transform.root.gameObject.GetComponentsInChildren(_bcType).Cast<MonoBehaviour>())
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
        List<Component> allSLs = new List<Component>();

        foreach(Module m in allModules)
        {
            Component sl = m.Obj.GetComponentInChildren(slType);
            allSLs.Add(sl);
            if(sl && !_SLToMod.ContainsKey(sl))
                _SLToMod.Add(sl, m.Obj);
        }

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
        if(TwitchPlaysActive)
        {
            _twitchMode = true;
            GameObject tpAPIGameObject = GameObject.Find("TwitchPlays_Info");
            if(tpAPIGameObject != null)
                _tpAPI = tpAPIGameObject.GetComponent<IDictionary<string, object>>();
            else
                _twitchMode = false;
        }
        else
            _twitchMode = false;

        if(_first && GetComponent<KMBombInfo>().GetSolvableModuleNames().Contains("Mystery Module"))
            Mystery();
    }

    private void Mystery()
    {
        Type mmt;
        Component[] mms = transform.root.GetComponentsInChildren(mmt = ReflectionHelper.FindType("MysteryModuleScript"));
        foreach(Component mm in mms)
            StartCoroutine(MysteryHook(mm, mmt));
    }

    private IEnumerator MysteryHook(Component mm, Type mmt)
    {
        FieldInfo fs = mmt.GetField("failsolve", ReflectionHelper.Flags);
        FieldInfo mdm = mmt.GetField("mystifiedModule", ReflectionHelper.Flags);
        yield return new WaitUntil(() => (bool)fs.GetValue(mm) || (KMBombModule)mdm.GetValue(mm));
        if((bool)fs.GetValue(mm))
            yield break;
        KMBombModule hidden = (KMBombModule)mdm.GetValue(mm);
        if(!hidden)
            yield break;

        Log("Located a Mystery Module! The hidden module will be removed.");
        Log("There are now {0} usable modules.", _submission - 1);

        ClearSLToMod();

        object hiddenSL = _SLToMod.First(kvp => ((Component)kvp.Value).GetComponent<KMBombModule>() == hidden).Key;
        if(_allLinks[hiddenSL] == hiddenSL)
        {
            _chains--;
            Log("The removal of the hidden module reduced the number of chains by one.");
            Log("There are now {0} chains.", _chains);
        }
        _allLinks[_allLinks.First(kvp => kvp.Value == hiddenSL).Key] = _allLinks[hiddenSL];
        _allLinks.Remove(hiddenSL);
        _submission--;
        foreach(SquadsShadowScript s in transform.root.GetComponentsInChildren<SquadsShadowScript>())
        {
            s._chains = _chains;
            s._submission = _submission;
            s.Press(-1);
        }

        hidden.OnPass += WarnSolve;
    }

    private void ClearSLToMod()
    {
        List<KeyValuePair<object, object>> sltm = _SLToMod.ToList();
        for(int i = _SLToMod.Count-1; i >= 0; i--)
        {
            try
            {
                if(sltm[i].Key == null || sltm[i].Value == null || ((Component)sltm[i].Key).gameObject == null || ((Component)sltm[i].Value).gameObject == null)
                    sltm.RemoveAt(i);
            }
            catch (NullReferenceException)
            {
                sltm.RemoveAt(i);
            }
        }
        _SLToMod = sltm.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
        _submission += 100;
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
        foreach(SquadsShadowScript s in transform.root.GetComponentsInChildren<SquadsShadowScript>())
            s.Warn(true);
        return false;
    }

    private bool UnwarnSolve(object o)
    {
        foreach(SquadsShadowScript s in transform.root.GetComponentsInChildren<SquadsShadowScript>())
            s.Warn(false);
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

        if(_twitchMode)
        {
            Component mod = _SLToMod[__instance] as Component;
            Component mod2 = _SLToMod[_allLinks[__instance]] as Component;
            if(mod == mod2)
                _tpAPI["ircConnectionSendMessage"] = "Solving Module " + GetModuleCode(mod) + " turned its own status light green.";
            else
                _tpAPI["ircConnectionSendMessage"] = "Solving Module " + GetModuleCode(mod) + " turned the status light on Module " + GetModuleCode(mod2) + " green.";
        }

        return false;
    }

    private bool TwitchPlaysActive;
    private static bool _twitchMode;
    private static IDictionary<string, object> _tpAPI;

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use ""! {0} submit 47"" to submit 47 chains.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parts = command.ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int submitted;
        if(parts.Length != 2 || parts[0] != "submit" || !int.TryParse(parts[1], out submitted) || submitted < 0 || submitted > 99)
            yield break;

        yield return null;
        if(submitted == _submission)
        {
            _buttons[0].OnInteract();
            yield return new WaitForSeconds(0.1f);
            _buttons[2].OnInteract();
            yield break;
        }

        int target1 = submitted / 10;
        int target2 = submitted % 10;

        int current1 = _submission / 10;
        int current2 = _submission % 10;

        int dir1 = current1 - target1 < 0 ? 0 : 2;
        int dir2 = current2 - target2 < 0 ? 1 : 3;

        while(_submission / 10 != target1)
        {
            _buttons[dir1].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while(_submission % 10 != target2)
        {
            _buttons[dir2].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        yield return "strike";
        yield return "solve";
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        Log("Module force solved.");
        IEnumerator cmd = ProcessTwitchCommand("submit " + _chains % 100);
        while(cmd.MoveNext())
            yield return cmd.Current;
        while(!_isSolved)
            yield return true;
    }

    private static string GetModuleCode(Component o)
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach(Transform child in o.transform.parent)
        {
            float distance = (o.transform.position - child.position).magnitude;
            if(child.gameObject.name == "TwitchModule(Clone)" && (closest == null || distance < closestDistance))
            {
                closest = child;
                closestDistance = distance;
            }
        }

        string name = "";
        KMBombModule m;
        if(m = o.GetComponent<KMBombModule>())
            name = m.ModuleDisplayName;
        else
            name = (string)_getDisplayName.Invoke(o, new object[0]);

        return closest != null ? closest.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text + " (" + name + ")" : "ERROR";
    }
}
