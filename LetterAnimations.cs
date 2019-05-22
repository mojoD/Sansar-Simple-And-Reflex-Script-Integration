/* This content is licensed under the terms of the Creative Commons Attribution 4.0 International License.
 * When using this content, you must:
 * •    Acknowledge that the content is from the Sansar Knowledge Base.
 * •    Include our copyright notice: "© 2017 Linden Research, Inc."
 * •    Indicate that the content is licensed under the Creative Commons Attribution-Share Alike 4.0 International License.
 * •    Include the URL for, or link to, the license summary at https://creativecommons.org/licenses/by-sa/4.0/deed.hi (and, if possible, to the complete license terms at https://creativecommons.org/licenses/by-sa/4.0/legalcode.
 * For example:
 * "This work uses content from the Sansar Knowledge Base. © 2017 Linden Research, Inc. Licensed under the Creative Commons Attribution 4.0 International License (license summary available at https://creativecommons.org/licenses/by/4.0/ and complete license terms available at https://creativecommons.org/licenses/by/4.0/legalcode)."
 */

using Sansar.Script;
using Sansar.Simulation;
using Sansar;
using System;
using System.Linq;
using System.Collections.Generic;

public class LetterAnimation : SceneObjectScript
{
    #region EditorProperties
    // Start playing on these events. Can be a comma separated list of event names.
    public string prefix = "L";
    public string EnableEvent = null;
    public string DisableEvent = null;

    #endregion

    #region SimpleHelpers v2
    // Update the region tag above by incrementing the version when updating anything in the region.

    // If a Group is set, will only respond and send to other SimpleScripts with the same Group tag set.
    // Does NOT accept CSV lists of groups.
    // To send or receive events to/from a specific group from outside that group prepend the group name with a > to the event name
    // my_group>on
    [DefaultValue("")]
    [DisplayName("Group")]
    public string Group = "";

    public interface ISimpleData
    {
        AgentInfo AgentInfo { get; }
        ObjectId ObjectId { get; }
        ObjectId SourceObjectId { get; }

        // Extra data
        Reflective ExtraData { get; }
    }

    public class SimpleData : Reflective, ISimpleData
    {
        public SimpleData(ScriptBase script) { ExtraData = script; }
        public AgentInfo AgentInfo { get; set; }
        public ObjectId ObjectId { get; set; }
        public ObjectId SourceObjectId { get; set; }

        public Reflective ExtraData { get; }
    }

    public interface IDebugger { bool DebugSimple { get; } }
    private bool __debugInitialized = false;
    private bool __SimpleDebugging = false;
    private string __SimpleTag = "";

    private string GenerateEventName(string eventName)
    {
        eventName = eventName.Trim();
        if (eventName.EndsWith("@"))
        {
            // Special case on@ to send the event globally (the null group) by sending w/o the @.
            return eventName.Substring(0, eventName.Length - 1);
        }
        else if (Group == "" || eventName.Contains("@"))
        {
            // No group was set or already targeting a specific group as is.
            return eventName;
        }
        else
        {
            // Append the group
            return $"{eventName}@{Group}";
        }
    }

    private void SetupSimple()
    {
        __debugInitialized = true;
        __SimpleTag = GetType().Name + " [S:" + Script.ID.ToString() + " O:" + ObjectPrivate.ObjectId.ToString() + "]";
        Wait(TimeSpan.FromSeconds(1));
        IDebugger debugger = ScenePrivate.FindReflective<IDebugger>("SimpleDebugger").FirstOrDefault();
        if (debugger != null) __SimpleDebugging = debugger.DebugSimple;
    }

    System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>> __subscribeActions = new System.Collections.Generic.Dictionary<string, Func<string, Action<ScriptEventData>, Action>>();
    private Action SubscribeToAll(string csv, Action<ScriptEventData> callback)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return null;

        Func<string, Action<ScriptEventData>, Action> subscribeAction;
        if (__subscribeActions.TryGetValue(csv, out subscribeAction))
        {
            return subscribeAction(csv, callback);
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __subscribeActions[csv] = SubscribeToAllInternal;
            return SubscribeToAllInternal(csv, callback);
        }

        // Chaining
        __subscribeActions[csv] = (_csv, _callback) =>
        {
            System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));

            string initial = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(initial);

            Action unsub = null;
            Action<ScriptEventData> wrappedCallback = null;
            wrappedCallback = (data) =>
            {
                string first = chainedCommands[0];
                chainedCommands.RemoveAt(0);
                chainedCommands.Add(first);
                if (unsub != null) unsub();
                unsub = SubscribeToAllInternal(first, wrappedCallback);
                Log.Write(LogLevel.Info, "CHAIN Subscribing to " + first);
                _callback(data);
            };

            unsub = SubscribeToAllInternal(initial, wrappedCallback);
            return unsub;
        };

        return __subscribeActions[csv](csv, callback);
    }

    private Action SubscribeToAllInternal(string csv, Action<ScriptEventData> callback)
    {
        Action unsubscribes = null;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (__SimpleDebugging)
        {
            Log.Write(LogLevel.Info, __SimpleTag, "Subscribing to " + events.Length + " events: " + string.Join(", ", events));
        }
        Action<ScriptEventData> wrappedCallback = callback;

        foreach (string eventName in events)
        {
            if (__SimpleDebugging)
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), (ScriptEventData data) =>
                {
                    Log.Write(LogLevel.Info, __SimpleTag, "Received event " + GenerateEventName(eventName));
                    wrappedCallback(data);
                });
                unsubscribes += sub.Unsubscribe;
            }
            else
            {
                var sub = SubscribeToScriptEvent(GenerateEventName(eventName), wrappedCallback);
                unsubscribes += sub.Unsubscribe;
            }
        }
        return unsubscribes;
    }

    System.Collections.Generic.Dictionary<string, Action<string, Reflective>> __sendActions = new System.Collections.Generic.Dictionary<string, Action<string, Reflective>>();
    private void SendToAll(string csv, Reflective data)
    {
        if (!__debugInitialized) SetupSimple();
        if (string.IsNullOrWhiteSpace(csv)) return;

        Action<string, Reflective> sendAction;
        if (__sendActions.TryGetValue(csv, out sendAction))
        {
            sendAction(csv, data);
            return;
        }

        // Simple case.
        if (!csv.Contains(">>"))
        {
            __sendActions[csv] = SendToAllInternal;
            SendToAllInternal(csv, data);
            return;
        }

        // Chaining
        System.Collections.Generic.List<string> chainedCommands = new System.Collections.Generic.List<string>(csv.Split(new string[] { ">>" }, StringSplitOptions.RemoveEmptyEntries));
        __sendActions[csv] = (_csv, _data) =>
        {
            string first = chainedCommands[0];
            chainedCommands.RemoveAt(0);
            chainedCommands.Add(first);

            Log.Write(LogLevel.Info, "CHAIN Sending to " + first);
            SendToAllInternal(first, _data);
        };
        __sendActions[csv](csv, data);
    }

    private void SendToAllInternal(string csv, Reflective data)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;
        string[] events = csv.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        if (__SimpleDebugging) Log.Write(LogLevel.Info, __SimpleTag, "Sending " + events.Length + " events: " + string.Join(", ", events) + (Group != "" ? (" to group " + Group) : ""));
        foreach (string eventName in events)
        {
            PostScriptEvent(GenerateEventName(eventName), data);
        }
    }
    #endregion

    private Animation animation = null;
    private AnimationParameters initialAnimationParameters;

    private AnimationComponent animComponent;

    public override void Init()
    {

        if (!ObjectPrivate.TryGetFirstComponent<AnimationComponent>(out animComponent))
        {
            Log.Write(LogLevel.Error, "NPCAnimation.Init", "Object must have an animation added at edit time for NPCAnimation to work");
            return;
        }

        animation = animComponent.DefaultAnimation;

        initialAnimationParameters = animation.GetParameters();
        //SubscribeToAll("Kill", KillAnimation);

        if (EnableEvent != "")
        {
            Log.Write("Enable Event was not null: " + EnableEvent);
            SubscribeToAll(EnableEvent, Subscribe);
        }
        else
        {
            Subscribe(null);  //executes it by passing null data
            SubscribeToAll(prefix + "0", N0);
            SubscribeToAll(prefix + "1", N1);
            SubscribeToAll(prefix + "2", N2);
            SubscribeToAll(prefix + "3", N3);
            SubscribeToAll(prefix + "4", N4);
            SubscribeToAll(prefix + "5", N5);
            SubscribeToAll(prefix + "6", N6);
            SubscribeToAll(prefix + "7", N7);
            SubscribeToAll(prefix + "8", N8);
            SubscribeToAll(prefix + "9", N9);
            SubscribeToAll(prefix + "A", A);
            SubscribeToAll(prefix + "B", B);
            SubscribeToAll(prefix + "C", C);
            SubscribeToAll(prefix + "D", D);
            SubscribeToAll(prefix + "E", E);
            SubscribeToAll(prefix + "F", F);
            SubscribeToAll(prefix + "G", G);
            SubscribeToAll(prefix + "H", H);
            SubscribeToAll(prefix + "I", I);
            SubscribeToAll(prefix + "J", J);
            SubscribeToAll(prefix + "K", K);
            SubscribeToAll(prefix + "L", L);
            SubscribeToAll(prefix + "M", M);
            SubscribeToAll(prefix + "N", N);
            SubscribeToAll(prefix + "O", O);
            SubscribeToAll(prefix + "P", P);
            SubscribeToAll(prefix + "Q", Q);
            SubscribeToAll(prefix + "R", R);
            SubscribeToAll(prefix + "S", S);
            SubscribeToAll(prefix + "T", T);
            SubscribeToAll(prefix + "U", U);
            SubscribeToAll(prefix + "V", V);
            SubscribeToAll(prefix + "W", W);
            SubscribeToAll(prefix + "X", X);
            SubscribeToAll(prefix + "Y", Y);
            SubscribeToAll(prefix + "Z", Z);
        }

        if (DisableEvent != "")
        {
            SubscribeToAll(DisableEvent, Unsubscribe);

        }
    }

    private void Subscribe(ScriptEventData sed)  //doesn't really pass data.  Always passes null
    {
        //Look At Animation Strings and subscribe to events
        Log.Write("In Subscribe");

        animation.JumpToFrame(0);
    }

    private void Unsubscribe(ScriptEventData sed)
    {

    }

    private void N0(ScriptEventData sed)
    {
        animation.JumpToFrame(0);
    }

    private void N1(ScriptEventData sed)
    {
        animation.JumpToFrame(1);
    }
    private void N2(ScriptEventData sed)
    {
        animation.JumpToFrame(2);
    }

    private void N3(ScriptEventData sed)
    {
        animation.JumpToFrame(3);
    }

    private void N4(ScriptEventData sed)
    {
        animation.JumpToFrame(4);
    }

    private void N5(ScriptEventData sed)
    {
        animation.JumpToFrame(5);
    }

    private void N6(ScriptEventData sed)
    {
        animation.JumpToFrame(6);
    }

    private void N7(ScriptEventData sed)
    {
        animation.JumpToFrame(7);
    }

    private void N8(ScriptEventData sed)
    {
        animation.JumpToFrame(8);
    }

    private void N9(ScriptEventData sed)
    {
        animation.JumpToFrame(9);
    }

    private void A(ScriptEventData sed)
    {
        animation.JumpToFrame(10);
    }

    private void B(ScriptEventData sed)
    {
        animation.JumpToFrame(11);
    }

    private void C(ScriptEventData sed)
    {
        animation.JumpToFrame(12);
    }

    private void D(ScriptEventData sed)
    {
        animation.JumpToFrame(13);
    }

    private void E(ScriptEventData sed)
    {
        animation.JumpToFrame(14);
    }

    private void F(ScriptEventData sed)
    {
        animation.JumpToFrame(15);
    }

    private void G(ScriptEventData sed)
    {
        animation.JumpToFrame(16);
    }
    private void H(ScriptEventData sed)
    {
        animation.JumpToFrame(17);
    }

    private void I(ScriptEventData sed)
    {
        animation.JumpToFrame(18);
    }

    private void J(ScriptEventData sed)
    {
        animation.JumpToFrame(19);
    }

    private void K(ScriptEventData sed)
    {
        animation.JumpToFrame(20);
    }

    private void L(ScriptEventData sed)
    {
        animation.JumpToFrame(21);
    }

    private void M(ScriptEventData sed)
    {
        animation.JumpToFrame(22);
    }

    private void N(ScriptEventData sed)
    {
        animation.JumpToFrame(23);
    }

    private void O(ScriptEventData sed)
    {
        animation.JumpToFrame(24);
    }

    private void P(ScriptEventData sed)
    {
        animation.JumpToFrame(25);
    }

    private void Q(ScriptEventData sed)
    {
        animation.JumpToFrame(26);
    }
    private void R(ScriptEventData sed)
    {
        animation.JumpToFrame(27);
    }

    private void S(ScriptEventData sed)
    {
        animation.JumpToFrame(28);
    }

    private void T(ScriptEventData sed)
    {
        animation.JumpToFrame(29);
    }

    private void U(ScriptEventData sed)
    {
        animation.JumpToFrame(30);
    }

    private void V(ScriptEventData sed)
    {
        animation.JumpToFrame(31);
    }
    private void W(ScriptEventData sed)
    {
        animation.JumpToFrame(32);
    }

    private void X(ScriptEventData sed)
    {
        animation.JumpToFrame(33);
    }

    private void Y(ScriptEventData sed)
    {
        animation.JumpToFrame(34);
    }

    private void Z(ScriptEventData sed)
    {
        animation.JumpToFrame(35);
    }

}


