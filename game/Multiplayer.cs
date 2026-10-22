using Godot;
using System.Text.Json;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private bool _networkActive, _isHost, _cooperative, _networkSignalsBound, _smokeRequestSent;
    private int _localSide;
    private readonly Dictionary<long, int> _sideByPeer = [];
    private void NetworkMenu()
    {
        ClearScreen(); Heading("Совместная игра"); var box = Panel(70, 200, 640, 570);
        var cooperative = new CheckBox { Text = "Кооператив: общий синий флот против AI", ButtonPressed = true }; box.AddChild(cooperative);
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Адрес хоста", CustomMinimumSize = new(0, 40) }; box.AddChild(address);
        Text(box, "Прототип: прямое подключение ENet, UDP-порт 24560. Комнаты по приглашению и relay — следующий этап.", 15, _colors.Muted);
        Button(box, "СОЗДАТЬ СКИРМИШ", () => { StartBattle("core:training", cooperative.ButtonPressed); StartNetwork(true, address.Text, 24560, cooperative.ButtonPressed); });
        Button(box, "СОЗДАТЬ КООП-КАМПАНИЮ", () =>
        {
            _campaign = FleetCommand.Core.Campaign.Start(_content, "core:patrol", ["host"]);
            StartBattle("core:training", true, preserveCampaign: true); StartNetwork(true, address.Text, 24560, true);
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
        if (!_networkSignalsBound)
        {
            Multiplayer.ConnectedToServer += () => RpcId(1, nameof(RegisterPeer), _content.Fingerprint);
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
    private void RegisterPeer(string fingerprint)
    {
        if (!_networkActive || !_isHost) return;
        long sender = Multiplayer.GetRemoteSenderId();
        if (fingerprint != _content.Fingerprint) { Multiplayer.MultiplayerPeer.DisconnectPeer((int)sender); return; }
        _sideByPeer[sender] = _cooperative ? 0 : 1;
        if (_campaign != null) _campaign = _campaign with { Participants = (_campaign.Participants ?? []).Append("peer-" + sender).Distinct().ToArray() };
        Broadcast();
        if (_args.Contains("--smoke-host") && !_cooperative) ApplyCommand(0, new("pass"));
    }
    private void Submit(BattleCommand command)
    {
        if (_state == null) return;
        if (ApplyTutorialCommand(command)) return;
        if (_networkActive && !_isHost)
            RpcId(1, nameof(RequestCommand), JsonSerializer.Serialize(command, JsonData.Options), _state.Sequence);
        else ApplyCommand(_networkActive ? _localSide : _bot ? 0 : _state.ActiveSide, command);
    }
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestCommand(string json, int sequence)
    {
        if (!_networkActive || !_isHost || _state == null || json.Length > 4096 || !_sideByPeer.TryGetValue(Multiplayer.GetRemoteSenderId(), out var side)) return;
        if (sequence != _state.Sequence) { Broadcast(); return; }
        try
        {
            var command = JsonSerializer.Deserialize<BattleCommand>(json, JsonData.Options);
            if (command != null) ApplyCommand(side, command);
            if (_args.Contains("--smoke-host") && _state.Sequence == (_cooperative ? 1 : 2))
            { GD.Print((_cooperative ? "COOP_" : "") + "NETWORK_COMMAND_ACCEPTED"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
        }
        catch (JsonException) { }
    }
    private void Broadcast()
    {
        if (_state == null) return;
        var saved = new SavedGame(_state, _generation, _campaign);
        var json = JsonSerializer.Serialize(saved, JsonData.Options);
        foreach (var peer in _sideByPeer.Keys) RpcId(peer, nameof(ReceiveSnapshot), json, _cooperative);
    }
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveSnapshot(string json, bool cooperative)
    {
        if (_isHost || json.Length > 2_000_000) return;
        try
        {
            var saved = JsonSerializer.Deserialize<SavedGame>(json, JsonData.Options) ?? throw new InvalidDataException("Empty snapshot.");
            if (saved.Generation != null) MapGenerator.Generate(_content, saved.Generation);
            _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
            _generation = saved.Generation; _campaign = saved.Campaign; _cooperative = cooperative; _localSide = cooperative ? 0 : 1;
            _status = "Синхронизация с хостом: действие " + _state.Sequence; DrawBattle();
            if (_args.Contains("--smoke-client") && _state.Sequence == (_cooperative ? 0 : 1) && !_smokeRequestSent) { _smokeRequestSent = true; Submit(new("pass")); }
            if (_args.Contains("--smoke-client") && _state.Sequence == (_cooperative ? 1 : 2))
            {
                if (_cooperative && _campaign?.Participants?.Length < 2) throw new InvalidDataException("Co-op campaign metadata missing.");
                GD.Print((_cooperative ? "COOP_" : "") + "NETWORK_SNAPSHOT_OK"); GetTree().Quit();
            }
        }
        catch (Exception e) when (e is JsonException or InvalidDataException) { GD.PushError(e.Message); StopNetwork(); }
    }
}
