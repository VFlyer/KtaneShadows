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
    private TextMesh _maxText, _bytesText, _percentText, _dateText, _timeText;
    [SerializeField]
    private Transform _greenBar, _grayBar;

    private readonly int _id = ++_idc;
    private static int _idc;

    private static List<Action<string>> _hooks = new List<Action<string>>();
    private Action<string> _hook;

    private static bool _harmed;

    private int _max, _bytesInternal;
    private int _bytes
    {
        get
        {
            return _bytesInternal;
        }
        set
        {
            _bytesInternal = value;
            float ratio = (float)_bytes / _max;
            ratio = Mathf.Clamp(ratio, 0f, 1f);
            _percentText.text = Math.Round(100f * ratio, 2) + "%";
            _greenBar.localScale = new Vector3(ratio, _greenBar.localScale.y, _greenBar.localScale.z);
            _grayBar.localScale = new Vector3(1f - ratio, _grayBar.localScale.y, _grayBar.localScale.z);
        }
    }

    private bool _interactable, _isSolved;

    private void Start()
    {
        HookHarmony();

        _hooks.Add(_hook = AnyPress);

        _max = RNG.Range(47, 69);

        Log("You have {0} total bytes of RAM.", _max);

        _maxText.text = _max.ToString();
        _bytesText.text = _bytes.ToString();

        _interactable = true;

        GetComponent<KMSelectable>().Children[0].OnInteract += ClearMemory;
        GetComponent<KMSelectable>().Children[1].OnInteract += Solve;

        StartCoroutine(DateTimeAnim());

        _bytes = 0;
    }

    private bool Solve()
    {
        _isSolved = true;
        Log("You pressed the solve button. Let's hope you don't use up too much memory...");
        GetComponent<KMBombModule>().HandlePass();
        _isSolved = true;

        return false;
    }

    private IEnumerator DateTimeAnim()
    {
        while(true)
        {
            _dateText.text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            _timeText.text = DateTime.Now.ToString("HH:mm");
            yield return null;
        }
    }

    private bool ClearMemory()
    {
        if(_isSolved)
            return false;

        _bytes = 0;

        Log("Memory reset.");

        return false;
    }

    private void HookHarmony()
    {
#if UNITY_EDITOR
        Log("Looks like you're in the editor. This module won't work here.");
        return;
#endif
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
        if(!_interactable)
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

        _bytes += used;

        Log("The button \"{0}\" was pressed. That used {1} byte{2} of memory. You are using {3} total.", button, used, plural, _bytes);

        if(_bytes > _max)
            StartCoroutine(Strike());
    }

    private IEnumerator Strike()
    {
        Log("That's too much! Module resetting...");
        _interactable = false;

        GetComponent<KMAudio>().PlaySoundAtTransform("4beeps", transform);

        yield return new WaitForSeconds(8f);

        GetComponent<KMBombModule>().HandleStrike();
        _bytes = 0;
        _interactable = !_isSolved;

        if(_isSolved)
            Log("That's the last of it.");
    }

    private void Log(string msg, params object[] args)
    {
        Debug.LogFormat("[Memory's Shadow #" + _id + "] " + msg, args);
    }

    private static void Postfix(MonoBehaviour __instance, Array ___Children)
    {
        if(___Children == null || ___Children.Length == 0)
            foreach(Action<string> h in _hooks)
                h(__instance.gameObject.name);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use ""!{0} clear"" to press the clear button. Use ""!{0} solve"" to press the solve button.";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        string[] acceptable = new string[] { "clear", "c", "press", "p", "push", "clear memory" };
        if(acceptable.Contains(command))
        {
            yield return null;
            GetComponent<KMSelectable>().Children[0].OnInteract();
        }
        if(command == "solve")
        {
            yield return null;
            GetComponent<KMSelectable>().Children[1].OnInteract();
            yield return "strike";
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        _interactable = false;
        Log("The module has been force solved.");
        GetComponent<KMSelectable>().Children[1].OnInteract();
        yield break;
    }
}
