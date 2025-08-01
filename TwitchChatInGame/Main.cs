using MelonLoader;
using TwitchTools;
using UnityEngine;
using System.Collections;
using RumbleModUI;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Combat.ShiftStones;
using Il2CppRUMBLE.Players;
using RumbleModdingAPI;
using Il2CppRUMBLE.MoveSystem;
using Il2CppRUMBLE.Pools;
using Il2CppTMPro;
using HarmonyLib;
using System.Globalization;
using UnityEngine.UI;
using Il2CppPhoton.Pun;

namespace TwitchChatInGame
{
    public static class BuildInfo
    {
        public const string ModName = "TwitchChatInGame";
        public const string ModVersion = "1.1.0";
        public const string Author = "UlvakSkillz";
    }

    public class ChatCommand
    {
        public string[] commands;
        private Func<object> functionToRun;
        public bool commandRan = false;
        public bool running = true;

        public ChatCommand(string[] newCommands, Func<object> newFunctionToRun)
        {
            commands = newCommands;
            functionToRun = newFunctionToRun;
        }

        public void RunCheck()
        {
            if (!running) { return; }
            foreach (string command in commands)
            {
                if (Main.chatEntries[0].Message.ToLower().Contains(command))
                {
                    try { functionToRun(); } catch { }
                    commandRan = true;
                    break;
                }
            }
        }
    }

    public class Main : MelonMod
    {
        private static string currentScene = "Loader";
        private static string FILEPATH = @"UserData\TwitchChatInGame";
        private static string FILENAME = @"ConnectionInfo.txt";
        private static string OVERRIDEFILENAME = @"PositionOverride.txt";
        private static string RESPONSESFILENAME = @"Responses.txt";
        private static string BLACKLISTEDNAMESFILENAME = @"BlackListedNames.txt";
        private static string BLACKLISTEDWORDSFILENAME = @"BlackListedWords.txt";
        private static List<string> blacklistedNames = new List<string>();
        private static List<string> blacklistedWords = new List<string>();
        private static string[] responsesFileText;
        private static string[] connectionInfo;
        private static TwitchChatHandle chat;
        public static List<TwitchChatEntry> chatEntries = new List<TwitchChatEntry>();
        private static bool fileFound = false;
        private static Mod TwitchChatInGame = new Mod();
        private static List<ChatCommand> commands = new List<ChatCommand>();
        private static GameObject screen;
        public static GameObject activeScreen;
        private static TextMeshProUGUI chatTMP;
        private static RawImage rawImage;
        private static bool showScreen = true;
        private static int whereToPinChat = 0;
        private static Vector3 overridePosition, overrideScale;
        private static Quaternion overrideRotation;
        private static bool overrideChatPosition = false;
        private static bool overrideChatRotation = false;
        private static bool overrideChatScale = false;
        private static bool toggleableChat = true;
        private static bool tapToSwitch = true;
        private static bool chatResponses = true;
        private static bool blockChatCommands = false;
        private static float spawningCooldownTime = 3f;
        private static string chatBackgroundColor = "FFFFFF";
        private static string chatTextColor = "000000";
        private static DateTime lastBall = DateTime.Now;
        private static DateTime lastPillar = DateTime.Now;
        private static DateTime lastCube = DateTime.Now;
        private static DateTime lastWall = DateTime.Now;
        private static DateTime lastSlow = DateTime.Now;
        private static DateTime lasttoggleChat = DateTime.Now;
        private static List<string> chatLog = new List<string>();
        private static bool lastKnownScreenActiveState = false;
        private static bool toggling = false;
        private static Vector3 originalScale;
        private static List<GameObject> screenSpots = new List<GameObject>();
        private static GameObject screenSpotsParent;
        private object processEntriesCoroutine, readEntriesCoroutine;

        [RegisterTypeInIl2Cpp]
        public class HandChecker : MonoBehaviour
        {
            private bool onLeft;
            private bool handTouching = false;

            public HandChecker()
            {
                try
                {
                    onLeft = this.transform.parent.name == "Bone_HandAlpha_L" ? true : false;
                }
                catch
                {
                    Component.Destroy(this);
                }
            }

            void OnTriggerEnter(Collider other)
            {
                try
                {
                    if (handTouching
                        || Main.toggling
                        || !Main.showScreen
                        || (Main.activeScreen == null)
                        || (onLeft && (other.gameObject.name != "Bone_HandAlpha_R"))
                        || (!onLeft && (other.gameObject.name != "Bone_HandAlpha_L"))
                        || (other.transform.parent.parent.parent.parent.parent.parent.parent.parent.parent.GetComponent<PlayerController>().controllerType != ControllerType.Local))
                    { return; }
                }
                catch { return; }
                if (Main.tapToSwitch && Main.toggleableChat && !Main.activeScreen.active && ((onLeft && Main.whereToPinChat == 2) || (!onLeft && Main.whereToPinChat == 1)))
                {
                    Main.whereToPinChat = onLeft ? 1 : 2;
                    Main.TwitchChatInGame.Settings[1].Value = Main.whereToPinChat;
                    Main.TwitchChatInGame.Settings[1].SavedValue = Main.whereToPinChat;
                    Main.SetActiveScreenTransform();
                    MelonCoroutines.Start(ToggleChat(true));
                }
                else if ((Main.toggleableChat)
                    && (((Main.whereToPinChat == 1) && (other.gameObject.name == "Bone_HandAlpha_R"))
                    || ((Main.whereToPinChat == 2) && (other.gameObject.name == "Bone_HandAlpha_L"))))
                {
                    handTouching = true;
                    MelonCoroutines.Start(waitForHoldPress());
                }
            }

            private IEnumerator waitForHoldPress()
            {
                yield return new WaitForSeconds(0.4f);
                try
                {
                    if (!toggling && handTouching) { MelonCoroutines.Start(ToggleChat(!Main.activeScreen.active)); }
                }
                catch { }
                yield break;
            }

            void OnTriggerExit(Collider other)
            {
                try
                {
                    if ((onLeft && (other.gameObject.name != "Bone_HandAlpha_R"))
                        || (!onLeft && (other.gameObject.name != "Bone_HandAlpha_L"))
                        || (other.transform.parent.parent.parent.parent.parent.parent.parent.parent.parent.GetComponent<PlayerController>().controllerType != ControllerType.Local))
                    { return; }
                }
                catch { return; }
                handTouching = false;
            }
        }

        public override void OnLateInitializeMelon()
        {
            TwitchChatInGame.ModName = "TwitchChatInGame";
            TwitchChatInGame.ModVersion = BuildInfo.ModVersion;
            TwitchChatInGame.SetFolder("TwitchChatInGame");
            TwitchChatInGame.ModSaved += Save;
            UI.instance.UI_Initialized += UIInit;
            FileInitialization();
            ModInitialization();
        }

        private void ModInitialization()
        {
            TwitchChatInGame.AddToList("Show Chat", true, 0, "Toggles ChatBox On/Off", new Tags { });
            TwitchChatInGame.AddToList("Where To Pin Chat", 1, $"0: Environment{Environment.NewLine}1: Left Hand{Environment.NewLine}2: Right Hand", new Tags { });
            TwitchChatInGame.AddToList("Chat Position Override", false, 0, "Toggles using the Chatbox Position Override from File On/Off" + Environment.NewLine + "Saving will Reload the Override with the Current File Info", new Tags { });
            TwitchChatInGame.AddToList("Chat Rotation Override", false, 0, "Toggles using the Chatbox Rotation Override from File On/Off" + Environment.NewLine + "Saving will Reload the Override with the Current File Info", new Tags { });
            TwitchChatInGame.AddToList("Chat Scale Override", false, 0, "Toggles using the Chatbox Scale Override from File On/Off" + Environment.NewLine + "Saving will Reload the Override with the Current File Info", new Tags { });
            TwitchChatInGame.AddToList("Chat Tap To Toggle", true, 0, "Toggles the Chatbox On/Off by Tapping the Back of the Wrist of the Hand that Chat is On", new Tags { });
            TwitchChatInGame.AddToList("Chat Tap To Switch", true, 0, "Toggles the Changing of what Hand the Chatbox is on by Tapping the Back of any Wrist while the Chatbox is Toggled OFF (From Setting Above this One)", new Tags { });
            TwitchChatInGame.AddToList("Chat Background Color", "FFFFFF", "Sets The Background Color to the Supplied Color", new Tags { });
            TwitchChatInGame.AddToList("Chat Text Color", "000000", "Sets The Text Color to the Supplied Color", new Tags { });
            TwitchChatInGame.AddToList("Chat Response Messages", true, 0, "Toggles The Bot Responding to Messages", new Tags { });
            TwitchChatInGame.AddToList("Block All Commands", false, 0, "Blocks all of the Chat Commands without needing to Toggle Each one Off", new Tags { });
            TwitchChatInGame.AddToList("Summon Commands CD", 3f, "Sets the Cooldown Time for !ball !pillar !cube !wall", new Tags { });
            TwitchChatInGame.AddToList("Reconnect Twitch", false, 0, "Reconnects to Twitch (For Settings Setup or Bot Timeout)", new Tags { DoNotSave = true });
            commands.Add(new ChatCommand(new string[] { "!commands" }, () => {
                string msg = responsesFileText[0];
                string commandList = "";
                bool first = true;
                for (int i = 1; i < commands.Count; i++)
                {
                    for (int j = 0; j < commands[i].commands.Length; j++)
                    {
                        if (commands[i].running)
                        {
                            if (!first) { commandList += ", "; }
                            else { first = false; }
                            commandList += commands[i].commands[j];
                        }
                    }
                }
                msg = msg.Replace("{commands}", commandList);
                Write(msg, true);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!aniyogev" }, () =>
            {
                Write(responsesFileText[1]);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!shiftstones" }, () => {
                string msg = responsesFileText[2];
                string shiftstones = "";
                PlayerShiftstoneSystem pss = PlayerManager.instance.localPlayer.Controller.gameObject.GetComponent<PlayerShiftstoneSystem>();
                string[] stones = new string[2];
                try { stones[0] = pss.shiftStoneSockets[0].assignedShifstone.StoneName; } catch { }
                try { stones[1] = pss.shiftStoneSockets[1].assignedShifstone.StoneName; } catch { }
                if (stones[0] != null) { shiftstones += stones[0]; }
                if (stones[1] != null) { shiftstones += " and " + stones[1]; }
                if ((stones[0] == null) && (stones[1] == null)) { shiftstones += "No Shiftstones"; }
                msg = msg.Replace("{shiftstones}", shiftstones);
                Write(msg);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!bp" }, () => {
                int bp = PlayerManager.instance.localPlayer.Data.GeneralData.BattlePoints;
                string msg = responsesFileText[3].Replace("{bp}", bp.ToString());
                Write(msg);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!region" }, () => {
                string regionCode = NetworkManager.instance.networkConfig.FixedRegion;
                List<string> regionCodes = new List<string>() { "asia", "au", "cae", "eu", "in", "jp", "ru", "rue", "za", "sa", "kr", "us", "usw" };
                string[] regionTitles = new string[13] { "Asia", "Australia", "Canada", "Europe", "India", "Japan", "Russia", "Russia, East", "South Africa", "South America", "South Korea", "USA, East", "USA, West" };
                string msg = responsesFileText[4].Replace("{region}", regionTitles[regionCodes.IndexOf(regionCode)]);
                Write(msg);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!opponent", "!opponents" }, () => {
                if ((PlayerManager.instance.AllPlayers.Count > 1) && ((currentScene == "Map0") || (currentScene == "Map1")))
                {
                    string name = PlayerManager.instance.AllPlayers[1].Data.GeneralData.PublicUsername;
                    int bp = PlayerManager.instance.AllPlayers[1].Data.GeneralData.BattlePoints;
                    string msg = responsesFileText[5].Replace("{otherplayer}", name).Replace("{otherbp}", bp.ToString());
                    Write(msg);
                }
                else
                {
                    Write($"@{chatEntries[0].Sender}, I am currently not fighting anyone.");
                }
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!hp", "!hps" }, () => {
                string[] msgSplit = responsesFileText[6].Split("|");
                string msg = msgSplit[0].Replace("{hp}", PlayerManager.instance.localPlayer.Data.HealthPoints.ToString());
                for (int i = 1; i < PlayerManager.instance.AllPlayers.Count; i++)
                {
                    Player player = PlayerManager.instance.AllPlayers[i];
                    msg += msgSplit[1].Replace("{otherplayer}", PlayerManager.instance.AllPlayers[i].Data.GeneralData.PublicUsername).Replace("{otherhp}", PlayerManager.instance.AllPlayers[i].Data.HealthPoints.ToString());
                }
                Write(msg);
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!mods" }, () =>
            {
                string msg = responsesFileText[7];
                List<int> bps = new List<int>();
                string mods = "";
                for (int i = 0; i < Calls.myMods.Count; i++)
                {
                    if (i != 0) { mods += ", "; }
                    if (i == Calls.myMods.Count - 1)
                    {
                        mods += " and ";
                    }
                    mods += Calls.myMods[i].ModName;
                }
                Write(msg.Replace("{mods}", mods));
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!ball" }, () =>
            {
                if ((currentScene == "Gym") || ((currentScene == "Park") && PhotonNetwork.IsMasterClient))
                {
                    MelonCoroutines.Start(ThrowBall());
                }
                else
                {
                    Write($"@{chatEntries[0].Sender}, use !ball only inside the Gym!");
                }
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!pillar" }, () =>
            {
                SpawnStruct("!pillar", "SpawnPillar");
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!cube" }, () =>
            {
                SpawnStruct("!cube", "SpawnCube");
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!wall" }, () =>
            {
                SpawnStruct("!wall", "SpawnWall");
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!slow" }, () =>
            {
                if ((currentScene == "Gym") || ((currentScene == "Park") && (PhotonNetwork.IsMasterClient)))
                {
                    if ((DateTime.Now - lastSlow).TotalSeconds > 5)
                    {
                        lastSlow = DateTime.Now;
                        MelonCoroutines.Start(SlowDownTime());
                        string msg = responsesFileText[12];
                        Write(msg);
                    }
                }
                else
                {
                    Write($"@{chatEntries[0].Sender}, use !slow only inside the Gym!");
                }
                return null;
            }));
            commands.Add(new ChatCommand(new string[] { "!togglechat" }, () =>
            {
                if (!toggling && (whereToPinChat != 0) && ((DateTime.Now - lasttoggleChat).TotalSeconds > 5))
                {
                    lasttoggleChat = DateTime.Now;
                    MelonCoroutines.Start(ToggleChat(!activeScreen.active));
                    string msg = responsesFileText[13];
                    Write(msg);
                }
                return null;
            }));
            for (int i = 0; i < commands.Count; i++)
            {
                TwitchChatInGame.AddToList(commands[i].commands[0], true, 0, $"Toggles On/Off {commands[i].commands[0]} Command", new Tags { });
            }
            TwitchChatInGame.GetFromFile();
            Save();
            screen = GameObject.Instantiate(Calls.LoadAssetFromStream<GameObject>(this, "TwitchChatInGame.twitchchat", "Canvas"));
            screen.name = "TwitchScreen";
            screen.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "";
            screen.SetActive(false);
            GameObject.DontDestroyOnLoad(screen);
            if (fileFound)
            {
                Log("Starting Twitch Bot");
                readEntriesCoroutine = MelonCoroutines.Start(ReadEntries());
                processEntriesCoroutine = MelonCoroutines.Start(ProcessEntries());
            }
        }

        private void FileInitialization()
        {
            if (!Directory.Exists(FILEPATH)) { Directory.CreateDirectory(FILEPATH); }
            blacklistedNames.Clear();
            if (!File.Exists($"{FILEPATH}\\{BLACKLISTEDNAMESFILENAME}")) { saveFile("", $"{FILEPATH}\\{BLACKLISTEDNAMESFILENAME}"); }
            string[] blacklistedNamesRaw = ReadFileText(FILEPATH, BLACKLISTEDNAMESFILENAME);
            foreach (string blacklistedName in blacklistedNamesRaw) { blacklistedNames.Add(blacklistedName.ToLower()); }
            blacklistedWords.Clear();
            if (!File.Exists($"{FILEPATH}\\{BLACKLISTEDWORDSFILENAME}")) { saveFile("", $"{FILEPATH}\\{BLACKLISTEDWORDSFILENAME}"); }
            string[] blacklistedWordsRaw = ReadFileText(FILEPATH, BLACKLISTEDWORDSFILENAME);
            foreach (string blacklistedWord in blacklistedWordsRaw) { blacklistedWords.Add(blacklistedWord.ToLower()); }
            if (!File.Exists($"{FILEPATH}\\{RESPONSESFILENAME}"))
            {
                string defaultText = "!commands|@{sender}, The Commands are: {commands}." + Environment.NewLine + "!aniyogev|@{sender}, I too wish AniYogev was here!" + Environment.NewLine + "!shiftstones|@{sender}, I am currently wearing {shiftstones}." + Environment.NewLine + "!bp|@{sender}, I currently have {bp} BP." + Environment.NewLine + "!region|@{sender}, I am Currently in {region}." + Environment.NewLine + "!opponent|@{sender}, I am currently fighting {otherplayer}, they have {otherbp} BP." + Environment.NewLine + "!hp|@{sender}, I currently have {hp} Health.| {otherplayer} has {otherhp} Health." + Environment.NewLine + "!mods|@{sender}, my Mods are: {mods}." + Environment.NewLine + "!ball|@{sender} used {command} to Summon a {structure}!" + Environment.NewLine + "!pillar|@{sender} used {command} to Summon a {structure}!" + Environment.NewLine + "!cube|@{sender} used {command} to Summon a {structure}!" + Environment.NewLine + "!wall|@{sender} used {command} to Summon a {structure}!" + Environment.NewLine + "!slow|@{sender} activated Slow Mo Time!" + Environment.NewLine + "!togglechat|@{sender}, are you trying to be Sneaky?";
                saveFile(defaultText, $"{FILEPATH}\\{RESPONSESFILENAME}");
                fileFound = false;
            }
            responsesFileText = ReadFileText(FILEPATH, RESPONSESFILENAME);
            for (int i = 0; i < responsesFileText.Length; i++)
            {
                string[] responses = responsesFileText[i].Split("|");
                responsesFileText[i] = responses[1];
                if (responses.Length > 2) { responsesFileText[i] += "|" + responses[2];}
            }
            if (!File.Exists($"{FILEPATH}\\{OVERRIDEFILENAME}"))
            {
                string defaultText = $"0 0 0{Environment.NewLine}0 0 0{Environment.NewLine}0.075 0.075 0.075{Environment.NewLine}{Environment.NewLine}--------------------------------------------------{Environment.NewLine}Line 1, Position (x y z){Environment.NewLine}Line 2, Rotation (x y z){Environment.NewLine}Line 3, Scale (x y z){Environment.NewLine}Put Spaces Between Each Number{Environment.NewLine}Save to Update Override while Game is open";
                saveFile(defaultText, $"{FILEPATH}\\{OVERRIDEFILENAME}");
                fileFound = false;
            }
            try
            {
                string[] fileText = ReadFileText(FILEPATH, OVERRIDEFILENAME);
                string[] text = (fileText[0] + " " + fileText[1] + " " + fileText[2]).Split(" ");
                overridePosition = new Vector3(float.Parse(text[0], CultureInfo.InvariantCulture), float.Parse(text[1], CultureInfo.InvariantCulture), float.Parse(text[2], CultureInfo.InvariantCulture));
                overrideRotation = Quaternion.Euler(float.Parse(text[3], CultureInfo.InvariantCulture), float.Parse(text[4], CultureInfo.InvariantCulture), float.Parse(text[5], CultureInfo.InvariantCulture));
                overrideScale = new Vector3(float.Parse(text[6], CultureInfo.InvariantCulture), float.Parse(text[7], CultureInfo.InvariantCulture), float.Parse(text[8], CultureInfo.InvariantCulture));
            }
            catch
            {
                overridePosition = Vector3.zero;
                overrideRotation = Quaternion.identity;
                overrideScale = Vector3.one;
            }
            TwitchFileCheck();
            if (!fileFound)
            {
                Log("File Info Not Set: " + $"{FILEPATH}\\{FILENAME}{Environment.NewLine}Please open File and edit to your info!");
            }
        }

        private void TwitchFileCheck()
        {
            if (!File.Exists($"{FILEPATH}\\{FILENAME}"))
            {
                string defaultText = "oauth:InsertOauthTokenHere" + Environment.NewLine + "ReplaceWithChannelName" + Environment.NewLine + Environment.NewLine + "--------------------------------------------------" + Environment.NewLine + "First Line is 'oauth:' followed by your oauth code (no space) (https://twitchtokengenerator.com/)." + Environment.NewLine + "Second Line is your Channel Name so that the bot goes to the correct spot.";
                saveFile(defaultText, $"{FILEPATH}\\{FILENAME}");
                fileFound = false;
            }
            connectionInfo = ReadFileText(FILEPATH, FILENAME);
            if ((connectionInfo[0] == "oauth:InsertOauthTokenHere") || (connectionInfo[1] == "ReplaceWithChannelName")) { fileFound = false; }
            else { fileFound = true; }
            chat = new TwitchChatHandle(connectionInfo[0], connectionInfo[1]);
        }

        private static IEnumerator ToggleChat(bool active)
        {
            toggling = true;
            originalScale = activeScreen.transform.localScale;
            if (active) { activeScreen.transform.localScale = Vector3.zero; }
            activeScreen.SetActive(true);
            for (int i = 0; i < 40; i++)
            {
                try
                {
                    if (active) { activeScreen.transform.localScale += originalScale / 40; }
                    else { activeScreen.transform.localScale -= originalScale / 40; }
                }
                catch { break; }
                yield return new WaitForFixedUpdate();
            }
            try
            {
                activeScreen.SetActive(active);
                lastKnownScreenActiveState = active;
                activeScreen.transform.localScale = originalScale;
            }
            catch { }
            toggling = false;
            yield break;
        }

        private static void SpawnStruct(string command, string structureName)
        {
            if ((currentScene == "Gym") || ((currentScene == "Park") && PhotonNetwork.IsMasterClient))
            {
                DateTime nextUse = DateTime.Now;
                string msg = "";
                switch (command)
                {
                    case "!ball":
                        msg += responsesFileText[8];
                        nextUse = lastBall.AddSeconds(spawningCooldownTime);
                        break;
                    case "!pillar":
                        msg += responsesFileText[9];
                        nextUse = lastPillar.AddSeconds(spawningCooldownTime);
                        break;
                    case "!cube":
                        msg += responsesFileText[10];
                        nextUse = lastCube.AddSeconds(spawningCooldownTime);
                        break;
                    case "!wall":
                        msg += responsesFileText[11];
                        nextUse = lastWall.AddSeconds(spawningCooldownTime);
                        break;
                }
                if (nextUse < DateTime.Now)
                {
                    switch (command)
                    {
                        case "!ball":
                            lastBall = DateTime.Now;
                            break;
                        case "!pillar":
                            lastPillar = DateTime.Now;
                            break;
                        case "!cube":
                            lastCube = DateTime.Now;
                            break;
                        case "!wall":
                            lastWall = DateTime.Now;
                            break;
                    }
                    PlayerStackProcessor psp = PlayerManager.instance.localPlayer.Controller.gameObject.GetComponent<PlayerStackProcessor>();
                    for (int i = 0; i < psp.availableStacks.Count; i++)
                    {
                        if (psp.availableStacks[i].name == structureName)
                        {
                            psp.Execute(psp.availableStacks[i]);
                            structureName = structureName.Replace("SpawnPillar", "Pillar").Replace("SpawnBall", "Ball").Replace("SpawnCube", "Cube").Replace("SpawnWall", "Wall");
                            msg = msg.Replace("{structure}", structureName).Replace("{command}", command);
                            Write(msg);
                            break;
                        }
                    }
                }
            }
            else
            {
                Write($"@{chatEntries[0].Sender}, use {command} only inside the Gym!");
            }
        }

        private IEnumerator SlowDownTime()
        {
            Time.timeScale = 0.5f;
            yield return new WaitForSecondsRealtime(5f);
            Time.timeScale = 1f;
        }

        private IEnumerator ThrowBall()
        {
            SpawnStruct("!ball", "SpawnBall");
            yield return new WaitForSeconds(1);
            Transform playerPos = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(0).GetChild(0);
            Il2CppSystem.Collections.Generic.List<PooledMonoBehaviour> pooledStructuresBall = PoolManager.instance.availablePools[PoolManager.instance.GetPoolIndex("Ball")].PooledObjects;
            Il2CppSystem.Collections.Generic.List<PooledMonoBehaviour> pooledStructures = new Il2CppSystem.Collections.Generic.List<PooledMonoBehaviour>();
            for (int i = 0; i < pooledStructuresBall.Count; i++) if (pooledStructuresBall[i].transform.gameObject.active) { { pooledStructures.Add(pooledStructuresBall[i]); } }
            if (pooledStructures.Count > 0)
            {
                int closest = -1;
                float closestDistance = 100f;
                for (int i = 0; i < pooledStructures.Count; i++)
                {
                    float distance = Vector2.Distance(new Vector2(pooledStructures[i].transform.position.x, pooledStructures[i].transform.position.z), new Vector2(playerPos.transform.position.x, playerPos.transform.position.z));
                    if (distance < closestDistance)
                    {
                        closest = i;
                        closestDistance = distance;
                    }
                }
                pooledStructures[closest].gameObject.GetComponent<Rigidbody>().AddForce((playerPos.position - pooledStructures[closest].gameObject.transform.position) * 15, ForceMode.VelocityChange);
            }
            yield break;
        }

        private void UIInit()
        {
            UI.instance.AddMod(TwitchChatInGame);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Time.timeScale = 1f;
            currentScene = sceneName;
        }

        private void saveFile(string textToSave, string file)
        {
            FileStream fs = File.Create(file);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(textToSave);
            fs.Write(bytes);
            fs.Close();
            fs.Dispose();
        }
        
        public static string[] ReadFileText(string filePath, string fileName)
        {
            try { return File.ReadAllLines($"{filePath}\\{fileName}"); }
            catch (Exception e) { MelonLogger.Error(e); }
            return null;
        }

        private void Save()
        {
            showScreen = (bool)TwitchChatInGame.Settings[0].SavedValue;
            if ((!showScreen) && (activeScreen != null)) { activeScreen.SetActive(false); }
            int pastWhereToPinChat = whereToPinChat;
            whereToPinChat = (int)TwitchChatInGame.Settings[1].SavedValue;
            overrideChatPosition = (bool)TwitchChatInGame.Settings[2].SavedValue;
            overrideChatRotation = (bool)TwitchChatInGame.Settings[3].SavedValue;
            overrideChatScale = (bool)TwitchChatInGame.Settings[4].SavedValue;
            toggleableChat = (bool)TwitchChatInGame.Settings[5].SavedValue;
            tapToSwitch = (bool)TwitchChatInGame.Settings[6].SavedValue;
            chatBackgroundColor = (string)TwitchChatInGame.Settings[7].SavedValue;
            chatTextColor = (string)TwitchChatInGame.Settings[8].SavedValue;
            chatResponses = (bool)TwitchChatInGame.Settings[9].SavedValue;
            blockChatCommands = (bool)TwitchChatInGame.Settings[10].SavedValue;
            spawningCooldownTime = (float)TwitchChatInGame.Settings[11].SavedValue;
            bool reconnect = (bool)TwitchChatInGame.Settings[12].SavedValue;
            if (reconnect)
            {
                TwitchChatInGame.Settings[12].Value = false;
                TwitchChatInGame.Settings[12].SavedValue = false;
                MelonCoroutines.Start(ReconnectBot());
            }
            for (int i = 0; i < commands.Count; i++) { commands[i].running = (bool)TwitchChatInGame.Settings[i + 13].SavedValue; }
            if ((pastWhereToPinChat != whereToPinChat) && (whereToPinChat == 0)) { MelonCoroutines.Start(MoveScreenIfNeeded()); }
            if (chatBackgroundColor.Length < 6)
            {
                Log("Chat Background Color too Short!");
                chatBackgroundColor = "FFFFFF";
                TwitchChatInGame.Settings[5].SavedValue = "FFFFFF";
                TwitchChatInGame.Settings[5].Value = "FFFFFF";
            }
            if (chatTextColor.Length < 6)
            {
                Log("Chat Text Color too Short!");
                chatBackgroundColor = "000000";
                TwitchChatInGame.Settings[6].SavedValue = "000000";
                TwitchChatInGame.Settings[6].Value = "000000";
            }
            SetActiveScreenTransform();
            LoadOverrideText();
            blacklistedNames.Clear();
            blacklistedWords.Clear();
            string[] blacklistedNamesRaw = ReadFileText(FILEPATH, BLACKLISTEDNAMESFILENAME);
            foreach (string blacklistedName in blacklistedNamesRaw) { blacklistedNames.Add(blacklistedName.ToLower()); }
            string[] blacklistedWordsRaw = ReadFileText(FILEPATH, BLACKLISTEDWORDSFILENAME);
            foreach (string blacklistedWord in blacklistedWordsRaw) { blacklistedWords.Add(blacklistedWord.ToLower()); }
        }

        private IEnumerator ReconnectBot()
        {
            MelonCoroutines.Stop(processEntriesCoroutine);
            MelonCoroutines.Stop(readEntriesCoroutine);
            yield return new WaitForSeconds(1f);
            chat.Dispose();
            TwitchFileCheck();
            if (fileFound)
            {
                while (chatEntries.Count > 0) { chatEntries.RemoveAt(0); }
                Log("Restarting Twitch Bot");
                readEntriesCoroutine = MelonCoroutines.Start(ReadEntries());
                processEntriesCoroutine = MelonCoroutines.Start(ProcessEntries());
            }
            yield break;
        }

        private void LoadOverrideText()
        {
            string[] fileText = ReadFileText(FILEPATH, OVERRIDEFILENAME);
            string[] text = (fileText[0] + " " + fileText[1] + " " + fileText[2]).Split(" ");
            overridePosition = new Vector3(float.Parse(text[0], CultureInfo.InvariantCulture), float.Parse(text[1], CultureInfo.InvariantCulture), float.Parse(text[2], CultureInfo.InvariantCulture));
            overrideRotation = Quaternion.Euler(float.Parse(text[3], CultureInfo.InvariantCulture), float.Parse(text[4], CultureInfo.InvariantCulture), float.Parse(text[5], CultureInfo.InvariantCulture));
            overrideScale = new Vector3(float.Parse(text[6], CultureInfo.InvariantCulture), float.Parse(text[7], CultureInfo.InvariantCulture), float.Parse(text[8], CultureInfo.InvariantCulture));
        }
        
        private static void SetActiveScreenTransform()
        {
            if (activeScreen == null) { return; }
            Transform newParent = null;
            Vector3 newPosition = Vector3.zero;
            Vector3 newScale = Vector3.one;
            Quaternion newRotation = Quaternion.identity;
            switch (whereToPinChat)
            {
                case 0: //Environment
                    try
                    {
                        newParent = screenSpots[0].transform;
                        if (showScreen)
                        {
                            if (!activeScreen.active) { ToggleChat(true); }
                        }
                        else
                        {
                            activeScreen.SetActive(false);
                        }
                    }
                    catch
                    {
                        newParent = null;
                    }
                    newPosition = new Vector3(0f, 0f, 0f);
                    newRotation = Quaternion.Euler(0f, 0f, 0f);
                    newScale = new Vector3(0.005f, 0.005f, 0.005f);
                    break;
                case 1: //Left Hand
                    newParent = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(1).GetChild(0).GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0);
                    newPosition = new Vector3(0.1f, 0f, 0f);
                    newRotation = Quaternion.Euler(0f, 270f, 30f);
                    newScale = new Vector3(0.0005f, 0.0005f, 0.0005f);
                    break;
                case 2: //Right Hand
                    newParent = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(1).GetChild(0).GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0);
                    newPosition = new Vector3(-0.1f, 0f, 0f);
                    newRotation = Quaternion.Euler(0f, 270f, 30f);
                    newScale = new Vector3(-0.0005f, 0.0005f, 0.0005f);
                    break;
            }
            if (newParent != null) { activeScreen.transform.parent = newParent; }
            activeScreen.transform.localPosition = overrideChatPosition ? overridePosition : newPosition;
            activeScreen.transform.localRotation = overrideChatRotation ? overrideRotation : newRotation;
            activeScreen.transform.localScale = overrideChatScale ? overrideScale : newScale;
            string chat = "";
            for (int i = 0; i < chatLog.Count; i++)
            {
                chat += chatLog[i];
                if (i < chatLog.Count - 1)
                {
                    chat += Environment.NewLine + Environment.NewLine;
                }
            }
            chatTMP.text = chat;
            rawImage.color = hexToColor(chatBackgroundColor);
            chatTMP.color = Color.white;
            chatTMP.faceColor = hexToColor(chatTextColor);
        }

        public static Color hexToColor(string hex)
        {
            hex = hex.Replace("0x", "");//in case the string is formatted 0xFFFFFF
            hex = hex.Replace("#", "");//in case the string is formatted #FFFFFF
            byte a = 255;//assume fully visible unless specified in hex
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            return new Color32(r, g, b, a);
        }

        private static void CheckCommands()
        {
            if (blockChatCommands) { return; }
            foreach (ChatCommand command in commands)
            {
                command.RunCheck();
                if (command.commandRan)
                {
                    command.commandRan = false;
                    break;
                }
            }
        }

        private static IEnumerator ProcessEntries()
        {
            Log("Starting Process Chat");
            while (fileFound)
            {
                if (chatEntries.Count > 0)
                {
                    try
                    {
                        if (chatEntries[0].Sender == "") { } //null check for on reconnect error
                    }
                    catch
                    {
                        chatEntries.RemoveAt(0);
                        continue;
                    }
                    Log(chatEntries[0].Sender + ": " + chatEntries[0].Message);
                    bool clear = true;
                    if (blacklistedNames.Contains(chatEntries[0].Sender.ToLower()))
                    {
                        clear = false;
                    }
                    else
                    {
                        foreach (string word in blacklistedWords)
                        {
                            if (chatEntries[0].Message.ToLower().Contains(word))
                            {
                                clear = false;
                                break;
                            }
                        }
                    }
                    if (clear)
                    {
                        CheckCommands();
                        UpdateChatLog();
                    }
                    chatEntries.RemoveAt(0);
                }
                else
                {
                    yield return new WaitForSeconds(0.25f);
                }
            }
            yield break;
        }

        private static void UpdateChatLog()
        {
            chatLog.Add(chatEntries[0].Sender + ": " + chatEntries[0].Message);
            while (chatLog.Count > 5) { chatLog.RemoveAt(0); }
            if (activeScreen != null)
            {
                string chat = "";
                for (int i = 0; i < chatLog.Count; i++)
                {
                    chat += chatLog[i];
                    if (i < chatLog.Count - 1)
                    {
                        chat += Environment.NewLine + Environment.NewLine;
                    }
                }
                chatTMP.text = chat;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Initialize", new Type[] { typeof(Player) })]
        public static class playerspawn
        {
            public static void Postfix(ref PlayerController __instance, ref Player player)
            {
                try
                {
                    if (player.Controller.controllerType != ControllerType.Local) { }
                }
                catch { return; }
                MelonCoroutines.Start(CreateScreen(__instance));
            }
        }

        private static IEnumerator CreateScreen(PlayerController playerController)
        {
            Player player = playerController.assignedPlayer;
            yield return new WaitForSeconds(1f);
            if ((playerController == null) || (player == null) || (player.Controller == null) || (player.Controller.controllerType != ControllerType.Local)) { yield break; }
            activeScreen = GameObject.Instantiate(screen);
            yield return new WaitForFixedUpdate();
            chatTMP = activeScreen.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            rawImage = activeScreen.transform.GetChild(0).GetComponent<RawImage>();
            SetActiveScreenTransform();
            activeScreen.SetActive(lastKnownScreenActiveState);
            GameObject[] chatToggles = new GameObject[2];
            chatToggles[0] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chatToggles[0].name = "TwitchChatToggle";
            chatToggles[0].layer = 22;
            Component.DestroyImmediate(chatToggles[0].GetComponent<MeshRenderer>());
            chatToggles[0].GetComponent<SphereCollider>().isTrigger = true;
            chatToggles[0].transform.parent = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(1).GetChild(0).GetChild(4).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0);
            chatToggles[0].transform.localPosition = new Vector3(0.03f, 0f, 0f);
            chatToggles[0].transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            chatToggles[0].transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
            chatToggles[0].AddComponent<HandChecker>();
            chatToggles[1] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            chatToggles[1].name = "TwitchChatToggle";
            chatToggles[1].layer = 22;
            Component.DestroyImmediate(chatToggles[1].GetComponent<MeshRenderer>());
            chatToggles[1].GetComponent<SphereCollider>().isTrigger = true;
            chatToggles[1].transform.parent = PlayerManager.instance.localPlayer.Controller.transform.GetChild(1).GetChild(1).GetChild(0).GetChild(4).GetChild(0).GetChild(2).GetChild(0).GetChild(0).GetChild(0);
            chatToggles[1].transform.localPosition = new Vector3(-0.03f, 0f, 0f);
            chatToggles[1].transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            chatToggles[1].transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
            chatToggles[1].AddComponent<HandChecker>();
            MelonCoroutines.Start(ScreenFade());
            MelonCoroutines.Start(MoveScreenIfNeeded());
            yield break;
        }

        private static IEnumerator MoveScreenIfNeeded()
        {
            Transform playerPos = PlayerManager.instance.localPlayer.Controller.gameObject.transform.GetChild(2).GetChild(0).GetChild(0);
            CreateScreenSpots();
            while ((activeScreen != null) && (whereToPinChat == 0))
            {
                if (showScreen) { activeScreen.SetActive(true); }
                int closest = -1;
                float distance = 100;
                for (int i = 0; i < screenSpots.Count; i++)
                {
                    float measuredDistance = Vector3.Distance(playerPos.transform.position, screenSpots[i].transform.position);
                    if (measuredDistance < distance)
                    {
                        closest = i;
                        distance = measuredDistance;
                    }
                }
                if ((screenSpots.Count > 0) && (activeScreen.transform.parent.gameObject != screenSpots[closest]))
                {
                    activeScreen.transform.parent = screenSpots[closest].transform;
                    activeScreen.transform.localPosition = overrideChatPosition ? overridePosition : Vector3.zero;
                    activeScreen.transform.localRotation = overrideChatRotation ? overrideRotation : Quaternion.identity;
                    activeScreen.transform.localScale = overrideChatScale ? overrideScale : new Vector3(0.005f, 0.005f, 0.005f);
                }
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        private static void CreateScreenSpots()
        {
            if (screenSpotsParent == null)
            {
                screenSpots.Clear();
                if (currentScene == "Map0")
                {
                    screenSpotsParent = new GameObject();
                    screenSpotsParent.name = "TwitchChatBoxSpots";
                    screenSpotsParent.transform.position = Vector3.zero;
                    screenSpotsParent.transform.rotation = Quaternion.identity;
                    screenSpotsParent.transform.localScale = Vector3.one;
                    GameObject spot0 = new GameObject();
                    spot0.name = "TwitchChatBoxSpot";
                    spot0.transform.parent = screenSpotsParent.transform;
                    spot0.transform.position = new Vector3(0f, 3.3564f, -17.784f);
                    spot0.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    spot0.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    screenSpots.Add(spot0);
                }
                else if (currentScene == "Map1")
                {
                    screenSpotsParent = new GameObject();
                    screenSpotsParent.name = "TwitchChatBoxSpots";
                    screenSpotsParent.transform.position = Vector3.zero;
                    screenSpotsParent.transform.rotation = Quaternion.identity;
                    screenSpotsParent.transform.localScale = Vector3.one;
                    GameObject spot0 = new GameObject();
                    spot0.name = "TwitchChatBoxSpot";
                    spot0.transform.parent = screenSpotsParent.transform;
                    spot0.transform.position = new Vector3(-14.1509f, 5.7582f, -2.8364f);
                    spot0.transform.rotation = Quaternion.Euler(0f, 244.3657f, 0f);
                    spot0.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    screenSpots.Add(spot0);
                }
                else if (currentScene == "Gym")
                {
                    screenSpotsParent = new GameObject();
                    screenSpotsParent.name = "TwitchChatBoxSpots";
                    screenSpotsParent.transform.position = Vector3.zero;
                    screenSpotsParent.transform.rotation = Quaternion.identity;
                    screenSpotsParent.transform.localScale = Vector3.one;
                    GameObject spot0 = new GameObject();
                    spot0.name = "TwitchChatBoxSpot";
                    spot0.transform.parent = screenSpotsParent.transform;
                    spot0.transform.position = new Vector3(-0.5035f, 1.5563f, -2.3288f); //spawn
                    spot0.transform.rotation = Quaternion.Euler(0f, 280.8829f, 0f);
                    spot0.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                    screenSpots.Add(spot0);
                    GameObject spot1 = new GameObject();
                    spot1.name = "TwitchChatBoxSpot";
                    spot1.transform.parent = screenSpotsParent.transform;
                    spot1.transform.position = new Vector3(15.5529f, -1.9519f, -12.4259f); //left of howard
                    spot1.transform.rotation = Quaternion.Euler(0f, 62.4684f, 0f);
                    spot1.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    screenSpots.Add(spot1);
                    GameObject spot2 = new GameObject();
                    spot2.name = "TwitchChatBoxSpot";
                    spot2.transform.parent = screenSpotsParent.transform;
                    spot2.transform.position = new Vector3(-15.2944f, 0.7143f, 2.0057f); //pose ghost
                    spot2.transform.rotation = Quaternion.Euler(0f, 290.1123f, 0f);
                    spot2.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    screenSpots.Add(spot2);
                    GameObject spot3 = new GameObject();
                    spot3.name = "TwitchChatBoxSpot";
                    spot3.transform.parent = screenSpotsParent.transform;
                    spot3.transform.position = new Vector3(-27.2884f, 4.2261f, 2.7007f); //back of gym
                    spot3.transform.rotation = Quaternion.Euler(0f, 97.1124f, 0f);
                    spot3.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    screenSpots.Add(spot3);
                    GameObject spot4 = new GameObject();
                    spot4.name = "TwitchChatBoxSpot";
                    spot4.transform.parent = screenSpotsParent.transform;
                    spot4.transform.position = new Vector3(-23.8473f, 0.9042f, -21.8762f); //targets
                    spot4.transform.rotation = Quaternion.Euler(0f, 258.3796f, 0f);
                    spot4.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    screenSpots.Add(spot4);
                }
                else if (currentScene == "Park")
                {
                    screenSpotsParent = new GameObject();
                    screenSpotsParent.name = "TwitchChatBoxSpots";
                    screenSpotsParent.transform.position = Vector3.zero;
                    screenSpotsParent.transform.rotation = Quaternion.identity;
                    screenSpotsParent.transform.localScale = Vector3.one;
                    GameObject spot0 = new GameObject();
                    spot0.name = "TwitchChatBoxSpot";
                    spot0.transform.parent = screenSpotsParent.transform;
                    spot0.transform.position = new Vector3(7.245f, -3.7736f, 3.8686f);
                    spot0.transform.rotation = Quaternion.Euler(0f, 63.7015f, 0f);
                    spot0.transform.localScale = Vector3.one;
                    screenSpots.Add(spot0);
                    GameObject spot1 = new GameObject();
                    spot1.name = "TwitchChatBoxSpot";
                    spot1.transform.parent = screenSpotsParent.transform;
                    spot1.transform.position = new Vector3(-29.3567f, -4.0141f, 7.6952f);
                    spot1.transform.rotation = Quaternion.Euler(0f, 253.9577f, 0f);
                    spot1.transform.localScale = Vector3.one;
                    screenSpots.Add(spot1);
                    GameObject spot2 = new GameObject();
                    spot2.name = "TwitchChatBoxSpot";
                    spot2.transform.parent = screenSpotsParent.transform;
                    spot2.transform.position = new Vector3(-16.175f, -1.6388f, -17.111f);
                    spot2.transform.rotation = Quaternion.Euler(0f, 163.0112f, 0f);
                    spot2.transform.localScale = Vector3.one;
                    screenSpots.Add(spot2);
                    GameObject spot3 = new GameObject();
                    spot3.name = "TwitchChatBoxSpot";
                    spot3.transform.parent = screenSpotsParent.transform;
                    spot3.transform.position = new Vector3(22.8701f, -1.7166f, -9.9974f);
                    spot3.transform.rotation = Quaternion.Euler(0f, 78.1961f, 0f);
                    spot3.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                    screenSpots.Add(spot3);
                    GameObject spot4 = new GameObject();
                    spot4.name = "TwitchChatBoxSpot";
                    spot4.transform.parent = screenSpotsParent.transform;
                    spot4.transform.position = new Vector3(34.8067f, 3.35f, 4.1303f);
                    spot4.transform.rotation = Quaternion.Euler(0f, 78.1961f, 0f);
                    spot4.transform.localScale = Vector3.one;
                    screenSpots.Add(spot4);
                    GameObject spot5 = new GameObject();
                    spot5.name = "TwitchChatBoxSpot";
                    spot5.transform.parent = screenSpotsParent.transform;
                    spot5.transform.position = new Vector3(20.7936f, 5.5078f, 27.1216f);
                    spot5.transform.rotation = Quaternion.Euler(0f, 43.9476f, 0f);
                    spot5.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    screenSpots.Add(spot5);
                    GameObject spot6 = new GameObject();
                    spot6.name = "TwitchChatBoxSpot";
                    spot6.transform.parent = screenSpotsParent.transform;
                    spot6.transform.position = new Vector3(-6.2776f, -1.6804f, 24.1848f);
                    spot6.transform.rotation = Quaternion.Euler(0f, 320.6074f, 0f);
                    spot6.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
                    screenSpots.Add(spot6);
                }
            }
            if (whereToPinChat == 0)
            {
                activeScreen.transform.parent = screenSpots[0].transform;
                activeScreen.transform.localPosition = Vector3.zero;
                activeScreen.transform.localRotation = Quaternion.identity;
                activeScreen.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
            }
        }

        private static IEnumerator ScreenFade()
        {
            Transform playerPos = PlayerManager.instance.localPlayer.Controller.gameObject.transform.GetChild(2).GetChild(0).GetChild(0);
            while (activeScreen != null)
            {
                Vector3 forwardVector = playerPos.forward.normalized;
                Vector3 directionToObject = (activeScreen.transform.position - playerPos.position).normalized;
                float dotProduct = Vector3.Dot(forwardVector, directionToObject);
                float angle = Mathf.Acos(dotProduct) * Mathf.Rad2Deg; // Convert radians to degrees
                float newAlpha = 0;
                if (angle <= 20f) { newAlpha = 1f; }
                else if (angle >= 45f) { newAlpha = 0f; }
                else { newAlpha = 1.8f - (0.04f * angle); }
                float inverter = -1;
                if (activeScreen.transform.parent == null) { inverter = -1; }
                else if (activeScreen.transform.parent.name == "Bone_HandAlpha_L") { inverter = -1; }
                else if (activeScreen.transform.parent.name == "Bone_HandAlpha_R") { inverter = 1; }
                Vector3 forwardVector2 = (activeScreen.transform.forward * inverter).normalized;
                Vector3 directionToObject2 = (playerPos.transform.position - activeScreen.transform.position).normalized;
                float dotProduct2 = Vector3.Dot(forwardVector2, directionToObject2);
                float angle2 = Mathf.Acos(dotProduct2) * Mathf.Rad2Deg; // Convert radians to degrees
                if (angle2 >= 90) { newAlpha = 0f; }
                else if (angle2 > 60)
                {
                    float newAlpha2 = 3f - ((1f / 30f) * angle2);
                    if (newAlpha > newAlpha2) { newAlpha = newAlpha2; }
                }
                rawImage.color = new Color(rawImage.color.r, rawImage.color.g, rawImage.color.b, newAlpha);
                chatTMP.color = new Color(chatTMP.color.r, chatTMP.color.g, chatTMP.color.b, newAlpha);
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        private IEnumerator ReadEntries()
        {
            Log("Starting Listening to Chat");
            while (fileFound)
            {
                TwitchChatEntry chatEntry = null;
                bool chatRead = false;
                Thread temp = new Thread(() =>
                {
                    chatEntry = chat.Read();
                    chatEntries.Add(chatEntry);
                    chatRead = true;
                });
                temp.Start();
                while (!chatRead)
                {
                    yield return new WaitForSeconds(0.25f);
                }
            }
            yield break;
        }

        public override void OnApplicationQuit()
        {
            if (fileFound) { Log("Stopping Twitch Bot"); }
            fileFound = false;
            chat.Dispose();
        }

        private static void Write(string msg, bool overrideResponses = false)
        {
            if (chatResponses || overrideResponses)
            {
                msg = msg.Replace("{sender}", chatEntries[0].Sender);
                int maxLength = 500;
                while (msg.Length > maxLength)
                {
                    string msgPart = msg.Substring(0, maxLength);
                    msg = msg.Substring(maxLength);
                    chat.Write(msgPart);
                }
                if (msg != "") { chat.Write(msg); }
            }
        }

        public static void Log(string msg)
        {
            MelonLogger.Msg(msg);
        }
    }
}
