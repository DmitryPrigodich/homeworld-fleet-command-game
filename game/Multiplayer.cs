using Godot;
using System.Text.Json;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private bool _networkActive, _isHost, _cooperative, _networkSignalsBound, _smokeRequestSent;
    private int _localSide;
    private int _specialNetworkStep = -1;
    private int _defenseNetworkStep;
    private int _capNetworkStep;
    private int _terrainNetworkStep;
    private int _economyNetworkStep;
    private int _logisticsNetworkStep;
    private int _objectiveNetworkStep;
    private int LocalCommandSide => _networkActive ? _localSide : _bot || _tutorial != null ? 0 : _state?.CapEntry is { } entry ? entry.Side : _state?.Overrun is { } overrun ? overrun.Side : _state?.Repairs is { Length: > 0 } ? _state.Repairs[0].Side : _state?.Combat is { } combat ? FleetCombat.DecisionSide(combat) : _state?.Defense is { } defense ? 1 - defense.Side : _state?.Cards?.Response is { } pause ? 1 - pause.Side
        : _state?.Cards is { Phase: "initiative" or "cleanup" } phase ? phase.Ready0 ? 1 : 0 : _state?.ActiveSide ?? 0;
    private readonly Dictionary<long, int> _sideByPeer = [];
    private void NetworkMenu()
    {
        ClearScreen(); Heading("Совместная игра"); var outer = Panel(70, 175, 720, 640);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 600), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 10); scroll.AddChild(box);
        var cooperative = new CheckBox { Text = "Кооператив: общий синий флот против AI", ButtonPressed = true }; box.AddChild(cooperative);
        var cardMode = new CheckBox { Text = "Учебный бой с картами" }; box.AddChild(cardMode);
        var objectives = new CheckBox { Text = "Цели и медали (учебная карта)" }; box.AddChild(objectives);
        var logistics = new CheckBox { Text = "Ангары и подкрепления (учебная карта)" }; box.AddChild(logistics);
        var economy = new CheckBox { Text = "Разведка и добыча (учебная карта)" }; box.AddChild(economy);
        var tabletop = new CheckBox { Text = "Механики правил (режим разработки)" }; box.AddChild(tabletop);
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Адрес хоста", CustomMinimumSize = new(0, 40) }; box.AddChild(address);
        Text(box, "Прототип: прямое подключение ENet, UDP-порт 24560. Комнаты по приглашению и relay — следующий этап.", 15, _colors.Muted);
        Button(box, "СОЗДАТЬ СКИРМИШ", () => { StartBattle(objectives.ButtonPressed ? BasicCardBattle.ObjectivesMapId : logistics.ButtonPressed ? BasicCardBattle.LogisticsMapId : economy.ButtonPressed ? BasicCardBattle.EconomyMapId : tabletop.ButtonPressed ? BasicCardBattle.TabletopMapId : cardMode.ButtonPressed ? BasicCardBattle.MapId : "core:training", cooperative.ButtonPressed); StartNetwork(true, address.Text, 24560, cooperative.ButtonPressed); QueueBot(); });
        var campaignChoices = _content.Campaigns.Values.ToArray(); var campaignPicker = new OptionButton(); foreach (var definition in campaignChoices) campaignPicker.AddItem("Кампания: " + definition.Name); box.AddChild(campaignPicker);
        Button(box, "СОЗДАТЬ КООП-КАМПАНИЮ", () =>
        {
            var campaign = campaignChoices[campaignPicker.Selected];
            _campaign = FleetCommand.Core.Campaign.Start(_content, campaign.Id, ["host"]);
            StartBattle(campaign.Missions.Single(m => m.Id == campaign.Start).Map, true, preserveCampaign: true); StartNetwork(true, address.Text, 24560, true);
        });
        Button(box, "ПРОДОЛЖИТЬ КООП-КАМПАНИЮ", () => { LoadGame(true); StartNetwork(true, address.Text, 24560, true); }, !System.IO.File.Exists(SavePath(true)));
        Button(box, "ПОДКЛЮЧИТЬСЯ", () => { StartBattle("core:training", false, persist: false); StartNetwork(false, address.Text, 24560, cooperative.ButtonPressed); });
        Button(box, "НАЗАД", MainMenu); MenuScene();
    }
    private void StartNetwork(bool host, string address, int port, bool cooperative)
    {
        StopNetwork();
        var peer = new ENetMultiplayerPeer();
        Error error = host ? peer.CreateServer(port, cooperative ? 3 : 1) : peer.CreateClient(address.Trim(), port);
        if (error != Error.Ok) throw new InvalidOperationException("Network: " + error);
        _networkActive = true; _isHost = host; _cooperative = cooperative; _localSide = host || cooperative ? 0 : 1;
        _bot = cooperative && host; Multiplayer.MultiplayerPeer = peer;
        if (host) InitializeCommanders();
        if (!_networkSignalsBound)
        {
            Multiplayer.ConnectedToServer += () => RpcId(1, nameof(RegisterPeer), "fleet-objectives-v1:" + _content.Fingerprint,
                JsonSerializer.Serialize(FleetPresentation.Normalize(_settings.Player), JsonData.Options));
            Multiplayer.ConnectionFailed += () => { _status = "Не удалось подключиться."; StopNetwork(); DrawBattle(); };
            Multiplayer.ServerDisconnected += () => { _status = "Хост отключился. Продолжение — из сохранения у хоста."; StopNetwork(); DrawBattle(); };
            Multiplayer.PeerDisconnected += id => { _sideByPeer.Remove(id); };
            _networkSignalsBound = true;
        }
        _status = host ? $"Хост запущен, UDP {port}. Можно подключаться." : "Подключение к хосту...";
        DrawBattle();
    }
    private void StopNetwork()
    {
        if (!_networkActive) return;
        _networkActive = false; _sideByPeer.Clear(); Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPeer(string fingerprint, string profileJson)
    {
        if (!_networkActive || !_isHost) return;
        long sender = Multiplayer.GetRemoteSenderId();
        if (fingerprint != "fleet-objectives-v1:" + _content.Fingerprint || profileJson.Length > 2048) { Multiplayer.MultiplayerPeer.DisconnectPeer((int)sender); return; }
        PlayerProfile profile;
        try { profile = FleetPresentation.Normalize(JsonSerializer.Deserialize<PlayerProfile>(profileJson, JsonData.Options)); }
        catch (JsonException) { Multiplayer.MultiplayerPeer.DisconnectPeer((int)sender); return; }
        _sideByPeer[sender] = _cooperative ? 0 : 1;
        if (!_cooperative)
        {
            var opponent = Commander(1);
            var color = FleetPresentation.DistinctColor(profile.Color, [Commander(0).Color]);
            _commanders[1] = opponent with { Name = profile.Name, Color = color,
                Emblem = profile.Emblem == "auto" ? FleetPresentation.DefaultEmblem(opponent.Faction) : profile.Emblem, IsNpc = false };
        }
        if (_campaign != null) _campaign = _campaign with { Participants = (_campaign.Participants ?? []).Append("peer-" + sender).Distinct().ToArray() };
        DrawBattle(); Broadcast(); SaveGame();
        if (_args.Contains("--smoke-terrain") || _args.Contains("--smoke-economy") || _args.Contains("--smoke-logistics") || _args.Contains("--smoke-objectives")) return;
        if (_args.Contains("--smoke-cap"))
        {
            ApplyCommand(0, new("attack", "s0-0", "s1-5", Card: "order-01"));
            if (_state!.Defense == null) ApplyCommand(1, new("decline"));
        }
        else if (_args.Contains("--smoke-defense"))
        {
            ApplyCommand(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            if (_state!.Defense == null) ApplyCommand(1, new("decline"));
            var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
            if (_state.Defense == null) throw new InvalidDataException("Defense window was not restored.");
            GD.Print("DEFENSE_NETWORK_RESUMED"); Broadcast();
        }
        else if (_args.Contains("--smoke-tabletop")) ApplyCommand(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
        else if (_args.Contains("--smoke-interrupt"))
        {
            ApplyCommand(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            var saved = JsonData.Read<SavedGame>(SavePath(false));
            _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
            if (_state.Cards?.Response == null) throw new InvalidDataException("Interrupt window was not saved.");
            GD.Print("INTERRUPT_NETWORK_RESUMED"); Broadcast();
        }
        else if (_args.Contains("--smoke-host") && !_cooperative) ApplyCommand(0, new(_args.Contains("--smoke-cards") ? "initiative" : "pass"));
    }
    private void Submit(BattleCommand command)
    {
        if (_state == null) return;
        if (ApplyTutorialCommand(command)) return;
        command = WithCard(command);
        if (_networkActive && !_isHost)
            RpcId(1, nameof(RequestCommand), JsonSerializer.Serialize(command, JsonData.Options), _state.Sequence);
        else ApplyCommand(LocalCommandSide, command);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestCommand(string json, int sequence)
    {
        if (!_networkActive || !_isHost || _state == null || json.Length > 4096 || !_sideByPeer.TryGetValue(Multiplayer.GetRemoteSenderId(), out var side)) return;
        if (sequence != _state.Sequence) { Broadcast(); return; }
        try
        {
            var command = JsonSerializer.Deserialize<BattleCommand>(json, JsonData.Options);
            if (command != null && !ApplyCommand(side, command)) RpcId(Multiplayer.GetRemoteSenderId(), nameof(ReceiveCommandFeedback), _status);
            if (_args.Contains("--smoke-objectives"))
            {
                if (command?.Kind == "attack")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(true)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Combat?.Kind != "board" || !FleetCombat.CanCapture(_content, _state) || _state.Medals1 != 0) throw new InvalidDataException("Objective capture decision was not restored.");
                    GD.Print("OBJECTIVE_CAPTURE_NETWORK_RESUMED"); Broadcast();
                }
                if (command?.Kind == "capture-result")
                {
                    if (!_state.Finished || _state.Medals1 != 3 || _state.Winner != 1) throw new InvalidDataException("Objective victory was not settled.");
                    _campaign = FleetCommand.Core.Campaign.CompleteMission(_content, _campaign!, _state); SaveGame();
                    var saved = JsonData.Read<SavedGame>(SavePath(true)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options)); _campaign = saved.Campaign;
                    if (_campaign?.OpponentMedals != 3 || _campaign.Results?.Length != 1) throw new InvalidDataException("Campaign medal history was not restored.");
                    GD.Print("OBJECTIVE_CAMPAIGN_NETWORK_RESUMED"); Broadcast(); GD.Print("OBJECTIVE_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-logistics"))
            {
                if (command?.Kind is "launch" or "build" or "deploy")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (command.Kind == "launch")
                    {
                        if (_state.Reinforce?.Mode != "launch" || _state.Units.Single(u => u.Id == "s1-5") is not { Activated: false, Launches: 1 }) throw new InvalidDataException("Launch state was not restored.");
                        GD.Print("LOGISTICS_LAUNCH_NETWORK_RESUMED");
                    }
                    if (command.Kind == "build")
                    {
                        if (!_state.Reserves!.Any(u => u.Id == "s1-10" && u.Embarked == "s1-5" && u.Activated) || _state.Funds1 != 2 || _state.Units.Single(u => u.Id == "s1-5").Cargo != 5) throw new InvalidDataException("Built unit or funding was not restored.");
                        GD.Print("LOGISTICS_BUILD_NETWORK_RESUMED");
                    }
                    if (command.Kind == "deploy") { GD.Print("LOGISTICS_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
                    Broadcast();
                }
                if (command?.Kind == "finish-reinforce") ApplyCommand(0, new("pass"));
                return;
            }
            if (_args.Contains("--smoke-economy"))
            {
                if (command?.Kind == "move")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Cards?.Pending is not { BasicAction: "harvest", Step: 1 } || FleetEconomy.KnownStars(_state, 2, 1) != 3 || BasicCardRules.View(_state, 0).Deposits![2].Stars != -1)
                        throw new InvalidDataException("Private survey or harvest continuation was not restored.");
                    GD.Print("ECONOMY_SURVEY_NETWORK_RESUMED"); Broadcast();
                }
                if (command?.Kind == "harvest") ApplyCommand(0, new("pass"));
                if (command?.Kind == "transfer-stars")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Units.Single(u => u.Id == "s1-3").Cargo != 1 || _state.Units.Single(u => u.Id == "s1-4").Cargo != 2 || _state.Harvested1 != 3 || _state.Stars1 != 0 || _state.Deposits![2] is not { Harvested: true, Stars: 3 }) throw new InvalidDataException("Cargo or public harvested values were not restored.");
                    GD.Print("ECONOMY_CARGO_NETWORK_RESUMED"); Broadcast(); GD.Print("ECONOMY_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-terrain"))
            {
                if (command?.Kind is "move" or "resolve")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (command.Kind == "move")
                    {
                        if (_state.Hazard?.Kind != "mine" || _state.Cards?.Response == null || _state.Defense == null) throw new InvalidDataException("Terrain movement window was not restored.");
                        GD.Print("TERRAIN_WINDOW_NETWORK_RESUMED");
                    }
                    else
                    {
                        if (_state.Combat is not { Kind: "terrain", AbilityRerolls: 1 } || _state.Combat.Rolls.Length != 5 || FleetTerrain.MineCount(_state, 0) != 5) throw new InvalidDataException("Terrain dice or deferred mine reduction were not restored.");
                        GD.Print("TERRAIN_ROLL_NETWORK_RESUMED");
                    }
                    Broadcast();
                }
                if (command?.Kind == "ability-reroll")
                {
                    for (int i = 0; i < 20 && _state.Hazard == null && _state.Combat == null && _state.Defense == null && _state.Cards?.Response is { Side: 1 }; i++) ApplyCommand(0, new("decline"));
                    if (_state.Cards?.Response != null || _state.Orders1 != 1 || FleetTerrain.MineCount(_state, 0) != 0 || _state.Units.Single(u => u.Id == "s1-3") is not { Q: 3, R: 2, FocusFire: 1 }) throw new InvalidDataException("Terrain movement did not resume exactly once.");
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (FleetTerrain.MineCount(_state, 0) != 0) throw new InvalidDataException("Cleared mines respawned after network save restore.");
                    Broadcast(); GD.Print("TERRAIN_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-cap"))
            {
                if (command?.Kind == "cap")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Combat?.Kind != "cap" || _state.Defense?.PreparedIncoming == null) throw new InvalidDataException("CAP simultaneous pools were not restored.");
                    GD.Print("CAP_ROLL_NETWORK_RESUMED"); Broadcast();
                }
                if (command?.Kind == "resolve" && _state.Combat?.Kind == "dogfight")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Defense?.CapVolley?.Ghost == null) throw new InvalidDataException("CAP confirmed volley was not restored.");
                    GD.Print("CAP_EXCHANGE_NETWORK_RESUMED"); ApplyCommand(0, new("resolve"));
                    if (_state.Units.Any(u => u.Id is "s0-0" or "s1-3") || _state.Cards?.Response != null) throw new InvalidDataException("Dogfight did not resolve simultaneous casualties.");
                    GD.Print("CAP_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-defense"))
            {
                if (command?.Kind == "defend")
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Combat?.Kind != "defense") throw new InvalidDataException("Rolled defense was not restored.");
                    GD.Print("DEFENSE_ROLL_NETWORK_RESUMED"); Broadcast();
                }
                if (command?.Kind == "resolve" && _state.Combat?.Kind == "ranged")
                {
                    if (_state.Combat.Rolls.Length != 3) throw new InvalidDataException("Missile pool was not reduced.");
                    ApplyCommand(0, new("resolve")); GD.Print("DEFENSE_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-tabletop"))
            {
                if (command?.Kind == "decline" && _state.Combat != null)
                {
                    while (_state.Combat is { AbilityRerolls: > 0 } ability) ApplyCommand(FleetCombat.DecisionSide(ability), new("ability-reroll", Dice: FleetCombat.EligibleDice(_content, _state).Take(ability.AbilityRerolls).ToArray()));
                    var saved = JsonData.Read<SavedGame>(SavePath(false)); _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    if (_state.Combat == null) throw new InvalidDataException("Post-roll save was not restored.");
                    GD.Print("TABLETOP_NETWORK_RESUMED"); ApplyCommand(0, new("valor"));
                    while (_state.Combat is { AbilityRerolls: > 0 } ability) ApplyCommand(FleetCombat.DecisionSide(ability), new("ability-reroll", Dice: FleetCombat.EligibleDice(_content, _state).Take(ability.AbilityRerolls).ToArray()));
                    if (_state.Combat != null) ApplyCommand(0, new("resolve"));
                    GD.Print("TABLETOP_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
                }
                return;
            }
            if (_args.Contains("--smoke-interrupt"))
            {
                if (command?.Kind == "interrupt" && _state.Cards?.Response == null && InterruptRules.Played(_state.Cards!, 1).Contains("order-17"))
                { GD.Print("INTERRUPT_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
                return;
            }
            if (_args.Contains("--smoke-cards"))
            {
                if (_args.Contains("--smoke-special") && _state.Cards?.Pending is { Step: 1 })
                {
                    var saved = JsonData.Read<SavedGame>(SavePath(false));
                    _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
                    GD.Print("SPECIAL_NETWORK_RESUMED");
                }
                if (_state.Cards is { Phase: "initiative", Ready0: false }) ApplyCommand(0, new("initiative"));
                if (_state.Cards is { Phase: "orders" } && _state.ActiveSide == 0) ApplyCommand(0, new("pass"));
                if (_args.Contains("--smoke-special") && command?.Kind == "finish-order") { GD.Print("SPECIAL_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
                if (command?.Kind == "pass") { GD.Print("CARD_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
            }
            if (!_args.Contains("--smoke-cards") && _args.Contains("--smoke-host") && _state.Sequence == (_cooperative ? 1 : 2))
            { GD.Print((_cooperative ? "COOP_" : "") + "NETWORK_COMMAND_ACCEPTED"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
        }
        catch (JsonException) { }
    }
    private void Broadcast()
    {
        if (_state == null) return;
        foreach (var peer in _sideByPeer.Keys)
        {
            var view = BasicCardRules.View(_state, _sideByPeer[peer]);
            var campaign = _campaign == null ? null : _campaign with { Battle = view };
            var saved = new SavedGame(view, _generation, campaign, _cooperative, _commanders);
            RpcId(peer, nameof(ReceiveSnapshot), JsonSerializer.Serialize(saved, JsonData.Options), _cooperative);
        }
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveCommandFeedback(string message)
    {
        if (_isHost || !_networkActive || message.Length > 4096) return;
        _status = message; DrawBattle();
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSnapshot(string json, bool cooperative)
    {
        if (_isHost || json.Length > 2_000_000) return;
        try
        {
            var saved = JsonSerializer.Deserialize<SavedGame>(json, JsonData.Options) ?? throw new InvalidDataException("Empty snapshot.");
            if (saved.Generation != null) MapGenerator.Generate(_content, saved.Generation);
            if (saved.State.Map == BasicCardBattle.MapId) BasicCardBattle.Register(_content);
            _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
            _generation = saved.Generation; _campaign = saved.Campaign; _cooperative = cooperative; _localSide = cooperative ? 0 : 1;
            _commanders = ValidateCommanders(saved.Commanders);
            _status = "Синхронизация с хостом: действие " + _state.Sequence; DrawBattle();
            if (_args.Contains("--smoke-cards"))
            {
                if (_state.Cards is not { IsView: true } cards || cards.Deck.Length != 0 || cards.Hand0.Length != 0 || _state.RandomState != 1)
                    throw new InvalidDataException("Private card state leaked over ENet.");
                if (_args.Contains("--smoke-objectives"))
                {
                    if (_objectiveNetworkStep == 2 && _state.Finished && _state.Winner == 1 && _state.Medals1 == 3 && _state.EndReason == "medals" && _campaign?.OpponentMedals == 3 && _campaign.Results is { Length: 1 })
                    {
                        if (_campaign.Battle?.Cards is not { IsView: true, Deck.Length: 0 } || _campaign.Battle.RandomState != 1) throw new InvalidDataException("Campaign objective snapshot leaked authority.");
                        GD.Print("OBJECTIVE_NETWORK_OK"); GetTree().Quit(); return;
                    }
                    if (_objectiveNetworkStep == 0 && cards.Phase == "orders" && _state.ActiveSide == 1) { _objectiveNetworkStep = 1; _attackKindChoice = "board"; Submit(new("attack", "s1-3", "s1-6", Card: "order-21", AttackKind: "board")); }
                    else if (_objectiveNetworkStep == 1 && _state.Combat?.Kind == "board") { _objectiveNetworkStep = 2; Submit(new("capture-result")); }
                    return;
                }
                if (_args.Contains("--smoke-logistics"))
                {
                    if (_logisticsNetworkStep == 7 && _state.Round == 2 && cards.Phase == "cleanup" && _state.Units.Any(u => u.Id == "s1-8" && u.Q == 9 && u.R == 0) && _state.Units.Single(u => u.Id == "s1-5") is { Cargo: 5, Launches: 0 } && _state.Funds1 == 2 && FleetLogistics.Children(_state, "s1-5").Length == 2)
                    { GD.Print("LOGISTICS_NETWORK_OK"); GetTree().Quit(); return; }
                    if (_logisticsNetworkStep == 0 && cards.Phase == "orders" && _state.ActiveSide == 1) { _logisticsNetworkStep = 1; Submit(new("begin-reinforce", "s1-5", "launch", Card: "order-21")); }
                    else if (_logisticsNetworkStep == 1 && _state.Reinforce?.Mode == "launch") { _logisticsNetworkStep = 2; Submit(new("launch", "s1-6", Q: 8, R: 2)); }
                    else if (_logisticsNetworkStep == 2 && _state.Units.Any(u => u.Id == "s1-6") && _state.Reinforce != null) { _logisticsNetworkStep = 3; Submit(new("finish-reinforce")); }
                    else if (_logisticsNetworkStep == 3 && _state.Reinforce == null && _state.ActiveSide == 1 && _state.Orders1 == 1) { _logisticsNetworkStep = 4; Submit(new("begin-reinforce", "s1-5", "build", Card: "order-22")); }
                    else if (_logisticsNetworkStep == 4 && _state.Reinforce?.Mode == "build") { _logisticsNetworkStep = 5; Submit(new("build", Definition: "practice:taiidan-wing", ToBay: true, ReserveSpend: 1)); }
                    else if (_logisticsNetworkStep == 5 && _state.Reinforce == null && _state.ActiveSide == 1 && _state.Orders1 == 2) { _logisticsNetworkStep = 6; Submit(new("begin-reinforce", "s1-8", "deploy", Card: "order-23")); }
                    else if (_logisticsNetworkStep == 6 && _state.Reinforce?.Mode == "deploy") { _logisticsNetworkStep = 7; Submit(new("deploy", Q: 9, R: 0)); }
                    return;
                }
                if (_args.Contains("--smoke-economy"))
                {
                    if (_state.Deposits == null || _state.Deposits[0].Stars != -1 || _state.Deposits[1].Stars != -1 || _state.Deposits.Values.Any(d => (d.SurveyMask & 1) != 0)) throw new InvalidDataException("Unscouted resources or opposing knowledge leaked over ENet.");
                    if (_economyNetworkStep == 4 && _state.Round == 2 && _state.Cards?.Phase == "cleanup" && _state.Units.Single(u => u.Id == "s1-3").Cargo == 1 && _state.Units.Single(u => u.Id == "s1-4").Cargo == 2 && _state.Deposits[2] is { Harvested: true, Stars: 3 })
                    { GD.Print("ECONOMY_NETWORK_OK"); GetTree().Quit(); return; }
                    if (_economyNetworkStep == 0 && cards.Pending == null && cards.Phase == "orders" && _state.ActiveSide == 1)
                    { if (_state.Deposits[2].Stars != -1) throw new InvalidDataException("Unsurveyed target was visible."); _economyNetworkStep = 1; Submit(new("begin-order", "s1-3", "harvest", Card: "order-21")); }
                    else if (_economyNetworkStep == 1 && cards.Pending is { BasicAction: "harvest", Step: 0 }) { _economyNetworkStep = 2; Submit(new("move", "s1-3", Q: 6, R: 1)); }
                    else if (_economyNetworkStep == 2 && cards.Pending is { BasicAction: "harvest", Step: 1 })
                    { if (_state.Deposits[2].Stars != 3 || _state.Deposits[2].Harvested) throw new InvalidDataException("Survey knowledge was lost."); _economyNetworkStep = 3; Submit(new("harvest", "s1-3", Object: 2)); }
                    else if (_economyNetworkStep == 3 && cards.Pending == null && _state.ActiveSide == 1 && _state.Orders1 == 1 && _state.Harvested1 == 3)
                    { _economyNetworkStep = 4; Submit(new("transfer-stars", "s1-4", "s1-3", Amount: 2, Receive: true, Card: "order-22")); }
                    return;
                }
                if (_args.Contains("--smoke-terrain"))
                {
                    if (_terrainNetworkStep == 4 && cards.Response == null && _state.Combat == null && _state.Defense == null && _state.Hazard == null && _state.Orders1 == 1 && FleetTerrain.MineCount(_state, 0) == 0 && _state.Units.Single(u => u.Id == "s1-3") is { Q: 3, R: 2, FocusFire: 1 })
                    { GD.Print("TERRAIN_NETWORK_OK"); GetTree().Quit(); return; }
                    if (_terrainNetworkStep == 0 && cards.Phase == "orders" && _state.ActiveSide == 1 && cards.Response == null && _state.Hazard == null)
                    { _terrainNetworkStep = 1; Submit(new("move", "s1-3", Q: 3, R: 2, Card: "order-21")); }
                    else if (_terrainNetworkStep == 1 && _state.Defense is { Fired: false }) { _terrainNetworkStep = 2; Submit(new("defend", Weapon: "guard")); }
                    else if (_terrainNetworkStep == 2 && _state.Combat?.Kind == "defense") { _terrainNetworkStep = 3; Submit(new("resolve")); }
                    else if (_terrainNetworkStep == 3 && _state.Combat is { Kind: "terrain", AbilityRerolls: > 0 }) { _terrainNetworkStep = 4; Submit(new("ability-reroll", Dice: [])); }
                    return;
                }
                if (_args.Contains("--smoke-cap"))
                {
                    if (_capNetworkStep == 2 && cards.Response == null && _state.Combat == null && _state.Defense == null && !_state.Units.Any(u => u.Id is "s0-0" or "s1-3"))
                    { GD.Print("CAP_NETWORK_OK"); GetTree().Quit(); return; }
                    if (_capNetworkStep == 0 && _state.Defense is { CapActive: false, CapComplete: false }) { _capNetworkStep = 1; Submit(new("cap", Card: "order-21", Weapon: "gun")); }
                    else if (_capNetworkStep == 1 && _state.Combat?.Kind == "cap") { _capNetworkStep = 2; Submit(new("resolve")); }
                    return;
                }
                if (_args.Contains("--smoke-defense"))
                {
                    if (_defenseNetworkStep == 2 && cards.Response == null && _state.Combat == null && _state.Defense == null && _state.Orders0 == 1)
                    { GD.Print("DEFENSE_NETWORK_OK"); GetTree().Quit(); return; }
                    if (_defenseNetworkStep == 0 && _state.Defense is { Fired: false }) { _defenseNetworkStep = 1; Submit(new("defend", Weapon: "flak")); }
                    else if (_defenseNetworkStep == 1 && _state.Combat?.Kind == "defense") { _defenseNetworkStep = 2; Submit(new("resolve")); }
                    return;
                }
                if (_args.Contains("--smoke-tabletop"))
                {
                    if (_smokeRequestSent && cards.Response == null && _state.Combat == null && _state.Orders0 == 1 && _state.ValorOwner == 1 && _state.ValorUsedMask == 1)
                    { GD.Print("TABLETOP_NETWORK_OK"); GetTree().Quit(); return; }
                    if (!_smokeRequestSent && cards.Response != null && _state.Combat == null) { _smokeRequestSent = true; Submit(new("decline")); }
                    return;
                }
                if (_args.Contains("--smoke-interrupt"))
                {
                    if (_smokeRequestSent && cards.Response == null && InterruptRules.Played(cards, 1).Contains("order-17"))
                    { GD.Print("INTERRUPT_NETWORK_OK"); GetTree().Quit(); return; }
                    if (!_smokeRequestSent && cards.Response != null)
                    { _smokeRequestSent = true; Submit(new("interrupt", "s1-3", "s0-0", Card: "order-17")); }
                    return;
                }
                if (_args.Contains("--smoke-special"))
                {
                    if (_smokeRequestSent && cards.Pending == null && _state.Log!.Any(e => e.Side == 1 && e.Text.Contains("Приказ завершён")))
                    { GD.Print("SPECIAL_NETWORK_OK"); GetTree().Quit(); return; }
                    if (cards.Pending is { } pending && pending.Step > _specialNetworkStep)
                    {
                        _specialNetworkStep = pending.Step;
                        if (pending.Step == 0)
                        {
                            var h = _engine.GetActions(_state, 1, SpecialOrders.Actor(pending)).MoveCells.First();
                            Submit(new("move", SpecialOrders.Actor(pending), Q: h.Q, R: h.R));
                        }
                        else Submit(new("finish-order"));
                        return;
                    }
                    if (cards.Phase == "orders" && _state.ActiveSide == 1 && !_smokeRequestSent)
                    { _smokeRequestSent = true; Submit(new("special", _state.Units.First(u => u.Side == 1).Id, Card: "order-01")); return; }
                }
                if (_smokeRequestSent && _state.Log!.Any(e => e.Side == 1 && e.Text.Contains("приказы пропущены"))) { GD.Print("CARD_NETWORK_OK"); GetTree().Quit(); return; }
                if (cards.Phase == "initiative" && !cards.Ready1) Submit(new("initiative"));
                if (cards.Phase == "orders" && _state.ActiveSide == 1 && !_smokeRequestSent) { _smokeRequestSent = true; Submit(new("pass")); }
                return;
            }
            if (_args.Contains("--smoke-client") && _state.Sequence == (_cooperative ? 0 : 1) && !_smokeRequestSent) { _smokeRequestSent = true; Submit(new("pass")); }
            if (_args.Contains("--smoke-client") && _state.Sequence == (_cooperative ? 1 : 2))
            {
                if (_cooperative && _campaign?.Participants?.Length < 2) throw new InvalidDataException("Co-op campaign metadata missing.");
                if (_cooperative ? !Commander(1).IsNpc || Commander(1).Color != FleetPresentation.NeutralColor :
                    Commander(0).Color == Commander(1).Color || Commander(1).IsNpc)
                    throw new InvalidDataException("Commander identities were not synchronized.");
                GD.Print("NETWORK_IDENTITIES_OK");
                if (_state.Log == null || !_state.Log.Any(entry => entry.Text.Contains("приказы пропущены")))
                    throw new InvalidDataException("Battle log was not synchronized.");
                GD.Print("NETWORK_LOG_OK");
                GD.Print((_cooperative ? "COOP_" : "") + "NETWORK_SNAPSHOT_OK"); GetTree().Quit();
            }
        }
        catch (Exception e) when (e is JsonException or InvalidDataException) { GD.PushError(e.Message); StopNetwork(); }
    }
}
