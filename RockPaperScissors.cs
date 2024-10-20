using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using StoreApi;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Menu;
using Menu.Enums;
using System.Text.Json;

namespace RPS;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("MinimumDuelBet")]
    public int MinimumDuelBet { get; set; } = 20;

    [JsonPropertyName("MaximumDuelBet")]
    public int MaximumDuelbet { get; set; } = 500;

    [JsonPropertyName("DuelCooldown")]
    public int DuelCoolown { get; set; } = 10;

    [JsonPropertyName("MenuType")]
    public string MenuType { get; set; } = "centerhtml";

    [JsonPropertyName("RequestTime")]
    public int RequestTime { get; set; } = 20;

    [JsonPropertyName("WinnerMultiplier")]
    public int WinnerMultiplier { get; set; } = 2;

    [JsonPropertyName("Commands")]
    public Commands Commands { get; set; } = new();
}
public class Commands
{
    public List<string> DuelCommand { get; set; } = ["duel"];
    public List<string> AcceptDuelCommand { get; set; } = ["duelaccept"];
    public List<string> RefuseDuelCommand { get; set; } = ["duelrefuse"];
}
public class Duel
{
    public CCSPlayerController? Challanger { get; set; }
    public CCSPlayerController? Challanged { get; set; }
    public int Bet { get; set; }
    public DateTime RequestTime { get; set; }

    public Duel(CCSPlayerController challanger, CCSPlayerController challanged, int bet)
    {
        Challanger = challanger;
        Challanged = challanged;
        Bet = bet;
        RequestTime = DateTime.Now;
    }
}

public class RockPaperScissors : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleAuthor => "T3Marius";
    public override string ModuleName => "[Store Module] Rock Paper Scissors";
    public override string ModuleVersion => "1.0";
    public IStoreApi? StoreApi { get; set; }

    private readonly ConcurrentDictionary<string, Duel> pendingDuel = new();
    private readonly ConcurrentDictionary<string, DateTime> playerLastChallange = new();
    private readonly ConcurrentDictionary<string, string> playerSelections = new();
    public PluginConfig Config { get; set; } = new PluginConfig();
    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
    }
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        StoreApi = IStoreApi.Capability.Get() ?? throw new Exception("StoreApi could not be located.");
        RegisterCommands();
    }
    public void RegisterCommands()
    {
        foreach (var cmd in Config.Commands.DuelCommand)
        {
            AddCommand($"css_{cmd}", "Duel someone", Command_Duel);
        }
        foreach (var cmd in Config.Commands.AcceptDuelCommand)
        {
            AddCommand($"css_{cmd}", "Accept Duel", Command_AcceptDuel);
        }
        foreach (var cmd in Config.Commands.RefuseDuelCommand)
        {
            AddCommand($"css_{cmd}", "Refuse Duel", Command_RefuseDuel);
        }
    }
    [CommandHelper(minArgs: 2, usage: "<player> <credits>")]
    public void Command_Duel(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (StoreApi == null)
            return;

        if (playerLastChallange.TryGetValue(player.SteamID.ToString(), out var lastChallange))
        {
            var cooldown = (DateTime.Now - lastChallange).TotalSeconds;
            if (cooldown < Config.DuelCoolown)
            {
                var remainingCooldown = (int)(Config.DuelCoolown - cooldown);
                info.ReplyToCommand(Localizer["Prefix"] + Localizer["Cooldown", remainingCooldown]);
                return;
            }
        }
        playerLastChallange[player.SteamID.ToString()] = DateTime.Now;

        var targetResult = info.GetArgTargetResult(1);
        var opponent = targetResult.Players.FirstOrDefault();

        if (opponent == null)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["NoPlayer"]);
            return;
        }
        if (!int.TryParse(info.GetArg(2), out int credits) || credits < 0)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["InvalidCredits"]);
            return;
        }
        if (credits < Config.MinimumDuelBet)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["MinBet", Config.MinimumDuelBet]);
            return;
        }
        if (credits > Config.MaximumDuelbet)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["MaxBet", Config.MaximumDuelbet]);
            return;
        }
        if (StoreApi.GetPlayerCredits(player) < credits)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["NoCredits"]);
            return;
        }

        var duelRequest = new Duel(player, opponent, credits);
        pendingDuel[opponent.SteamID.ToString()] = duelRequest;

        var duel = new Duel(player, opponent, credits);
        pendingDuel[opponent.SteamID.ToString()] = duel;

        player.PrintToChat(Localizer["Prefix"] + Localizer["DuelSendMessage", opponent.PlayerName, duel.Bet]);
        opponent.PrintToChat(Localizer["Prefix"] + Localizer["DuelMessage", player.PlayerName, duel.Bet]);


        AddTimer(Config.RequestTime, () =>
        {
            if (pendingDuel.TryRemove(opponent.SteamID.ToString(), out var duel))
            {
                duel.Challanger?.PrintToChat(Localizer["Prefix"] + Localizer["ChallangeTimeOut"]);
            }
        });
    }
    public void Command_AcceptDuel(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;
        if (StoreApi == null)
            return;

        if (!pendingDuel.TryGetValue(player.SteamID.ToString(), out var duel)) // Changed to `!`
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["NoDuel"]); // No duel found
            return;
        }

        if (StoreApi.GetPlayerCredits(player) < duel?.Bet)
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["NoCreditsAcc"]);
            return;
        }

        OpenRPSMenu(duel?.Challanger, duel?.Challanged, duel!.Bet);
    }

    private void OpenRPSMenu(CCSPlayerController? player1, CCSPlayerController? player2, int bet)
    {
        if (player1 == null || player2 == null)
            return;

        switch(Config.MenuType.ToLower())
        {
            case "centerhtml":
                OpenHtmlMenu(player1, player2, bet);
                OpenHtmlMenu(player2, player1, bet);
                break;
            case "chat":
                OpenChatMenu(player1, player2, bet);
                OpenChatMenu(player2, player1, bet);
                break;
            case "kitsune":
                OpenKitsuneMenu(player1, player2, bet);
                OpenKitsuneMenu(player2, player1, bet);
                break;
        }
    }

    private void OpenHtmlMenu(CCSPlayerController player, CCSPlayerController opponent, int bet)
    {
        if (player == null || opponent == null)
            return;

        CenterHtmlMenu menu = new CenterHtmlMenu(Localizer["menu<title>"], this)
        {
            PostSelectAction = PostSelectAction.Close
        };

        menu.AddMenuOption(Localizer["Rock"], (player, option) => HandlePlayerSelection(player, opponent, "Rock", bet));
        menu.AddMenuOption(Localizer["Paper"], (player, option) => HandlePlayerSelection(player, opponent, "Paper", bet));
        menu.AddMenuOption(Localizer["Scissors"], (player, option) => HandlePlayerSelection(player, opponent, "Scissors", bet));

        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }
    private void OpenChatMenu(CCSPlayerController player, CCSPlayerController opponent, int bet)
    {
        if (player == null || opponent == null)
            return;

        ChatMenu menu = new ChatMenu(Localizer["menu<title>"]);

        menu.AddMenuOption(Localizer["Rock"], (player, option) => HandlePlayerSelection(player, opponent, "Rock", bet));
        menu.AddMenuOption(Localizer["Paper"], (player, option) => HandlePlayerSelection(player, opponent, "Paper", bet));
        menu.AddMenuOption(Localizer["Scissors"], (player, option) => HandlePlayerSelection(player, opponent, "Scissors", bet));
    }
    private void OpenKitsuneMenu(CCSPlayerController player, CCSPlayerController opponent, int bet)
    {
        if (player == null || opponent == null)
            return;

        var menu = new KitsuneMenu(this);
        List<MenuItem> menuItems = new List<MenuItem>
        {
            new MenuItem(MenuItemType.Button, new List<MenuValue> {new MenuValue(Localizer["Rock"])}),
            new MenuItem(MenuItemType.Button, new List<MenuValue> {new MenuValue(Localizer["Paper"])}),
            new MenuItem(MenuItemType.Button, new List<MenuValue> {new MenuValue(Localizer["Scissors"])})
        };

        menu.ShowScrollableMenu(player, Localizer["menu<title>"], menuItems, (menuButtons, menu, selectedItem) =>
        {
            if (selectedItem == null || selectedItem.Values == null || selectedItem.Values.Count == 0)
                return;

            if (menuButtons == MenuButtons.Exit)
                return;

            string selectedOption = selectedItem.Values[0].Value;

            if (selectedOption == Localizer["Rock"])
                HandlePlayerSelection(player, opponent, "Rock", bet);
            else if (selectedOption == Localizer["Paper"])
                HandlePlayerSelection(player, opponent, "Paper", bet);
            else if (selectedOption == Localizer["Scissors"])
                HandlePlayerSelection(player, opponent, "Scissors", bet);

        }, false, true);
    }
    private void HandlePlayerSelection(CCSPlayerController player, CCSPlayerController opponent, string choice, int bet)
    {
        playerSelections[player.SteamID.ToString()] = choice;

        if (playerSelections.ContainsKey(opponent.SteamID.ToString()))
        {
            string playerChoche = playerSelections[player.SteamID.ToString()];
            string opponentChoice = playerSelections[opponent.SteamID.ToString()];

            DetermineWinner(player, opponent, playerChoche, opponentChoice, bet);

            playerSelections.TryRemove(player.SteamID.ToString(), out _);
            playerSelections.TryRemove(opponent.SteamID.ToString(), out _);
        }
    }
    private void DetermineWinner(CCSPlayerController player1, CCSPlayerController player2, string player1Choice, string player2Choice, int bet)
    {
        if (StoreApi == null)
            return;

        string resultMessage;

        if (player1Choice == player2Choice)
        {
            resultMessage = Localizer["Tie"];
            player1.PrintToChat(Localizer["prefix"] + resultMessage);
            player2.PrintToChat(Localizer["prefix"] + resultMessage);

            StoreApi.GivePlayerCredits(player1, bet);
            StoreApi.GivePlayerCredits(player2, bet);
        }
        else
        {
            bool player1Wins = (player1Choice == "Rock" && player2Choice == "Scissors") ||
                               (player1Choice == "Scissors" && player2Choice == "Paper") ||
                               (player1Choice == "Paper" && player2Choice == "Rock");

            if (player1Wins)
            {
                resultMessage = Localizer["Win", player1.PlayerName, player1Choice, player2.PlayerName, player2Choice, player1.PlayerName];
                player1.PrintToChat(Localizer["prefix"]+ resultMessage);
                player2.PrintToChat(Localizer["prefix"]+ resultMessage);

                StoreApi.GivePlayerCredits(player1, bet * Config.WinnerMultiplier);
            }
            else
            {
                resultMessage = Localizer["Win", player2.PlayerName, player2Choice, player1.PlayerName, player1Choice, player2.PlayerName];
                player1.PrintToChat(Localizer["prefix"] + resultMessage);
                player2.PrintToChat(Localizer["prefix"] + resultMessage);

                StoreApi.GivePlayerCredits(player2, bet * Config.WinnerMultiplier);
            }
        }
    }
    public void Command_RefuseDuel(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null)
            return;

        if (pendingDuel.TryRemove(player.SteamID.ToString(), out var duel))
        {
            duel.Challanger?.PrintToChat(Localizer["Prefix"] + Localizer["DuelRefused", player.PlayerName]);
            player.PrintToChat(Localizer["Prefix"] + Localizer["DuelRefuse", duel.Challanger!.PlayerName]);
        }
        else
        {
            info.ReplyToCommand(Localizer["Prefix"] + Localizer["NoDuelToRefuse"]);
        }
    }

}