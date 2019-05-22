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

public class NPCTeleport : SceneObjectScript
{
    #region EditorProperties
    // Teleport the person who interacted to somewhere within the scene. Can be a comma separated list of event names.
    [DefaultValue("on")]
    [DisplayName("-> Local Teleport")]
    public readonly string LocalEvent;

    // The destination within the scene to teleport to
    [DisplayName("Destination")]
    [DefaultValue("<0,0,0>")]
    public readonly Vector Destination;

    // If true the destination position is relative to the object this script is on
    // If false the destination is in scene coordinates regardless of this script's object's position
    [DefaultValue(true)]
    [DisplayName("Relative Position")]
    public readonly bool RelativeDestination;

    // Teleport the person that interacted to a remote scene. Can be a comma separated list of event names.
    [DefaultValue("")]
    [DisplayName("-> Remote Teleport")]
    public readonly string RemoteEvent;

    // The destination scene owner (from the scene url)
    [DisplayName("Dest. Owner")]
    [DefaultValue("")]
    public readonly String DestOwner;

    // The destination scene handle (from the scene url)
    [DefaultValue("")]
    [DisplayName("Dest. Scene")]
    public readonly String DestScene;

    // Enable responding to events for this script. Can be a comma separated list of event names.
    [DefaultValue("tp_enable")]
    [DisplayName("-> Enable")]
    public readonly string EnableEvent;

    // Disable responding to events for this script. Can be a comma separated list of event names.
    [DefaultValue("tp_disable")]
    [DisplayName("-> Disable")]
    public readonly string DisableEvent;

    // If StartEnabled is true then the script will respond to events when the scene is loaded
    // If StartEnabled is false then the script will not respond to events until an (-> Enable) event is received.
    [DefaultValue(true)]
    [DisplayName("Start Enabled")]
    public readonly bool StartEnabled = true;
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

    Action Unsubscribes = null;
    public override void Init()
    {
        if (StartEnabled) Subscribe(null);

        SubscribeToAll(EnableEvent, Subscribe);
        SubscribeToAll(DisableEvent, Unsubscribe);
    }

    private void Subscribe(ScriptEventData sed)
    {
        if (Unsubscribes == null)
        {
            Unsubscribes = SubscribeToAll(LocalEvent, LocalTeleport);
            Unsubscribes += SubscribeToAll(RemoteEvent, RemoteTeleport);
        }
    }

    private void Unsubscribe(ScriptEventData sed)
    {
        if (Unsubscribes != null)
        {
            Unsubscribes();
            Unsubscribes = null;
        }
    }

    private void LocalTeleport(ScriptEventData data)
    {
        Log.Write("A");
        foreach (AgentPrivate agent in ScenePrivate.GetAgents())
        {
            Log.Write(agent.AgentInfo.Name);
            if (agent.AgentInfo.Name == "GranddadGotMojo")
            {
                Log.Write("Camaeraman found");
                ObjectPrivate objectPrivate = ScenePrivate.FindObject(agent.AgentInfo.ObjectId);
                if (objectPrivate != null)
                {
                    AnimationComponent anim = null;
                    if (objectPrivate.TryGetFirstComponent(out anim))
                    {
                        if (RelativeDestination)
                        {
                            anim.SetPosition(Destination + ObjectPrivate.Position);
                        }
                        else
                        {
                            anim.SetPosition(Destination);
                        }
                    }
                }
            }
        }
    }

    private void RemoteTeleport(ScriptEventData data)
    {
        ISimpleData idata = data.Data.AsInterface<ISimpleData>();
        AgentPrivate agent = ScenePrivate.FindAgent(idata.AgentInfo.SessionId);
        if (agent != null)
        {
            agent.Client.TeleportToLocation(DestOwner, DestScene);
        }
    }
}