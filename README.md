# Require:
https://github.com/roflmuffin/CounterStrikeSharp

[https://github.com/roflmuffin/CounterStrikeSharp](https://github.com/schwarper/cs2-store)


# Information
**Commands:**
```
!duel <playername> <credits>
!duelaccept 
!duelrefuse

Keep in mind these are editable in config file!
```

# Config
```
{
  "MinimumDuelBet": 20,
  "MaximumDuelBet": 500,
  "DuelCooldown": 10,
  "MenuType": "centerhtml", // options: kitsune, chat, centerhtml
  "RequestTime": 20,
  "WinnerMultiplier": 2,
  "Commands": {
    "DuelCommand": [ "duel" ],
    "AcceptDuelCommand": [ "duelaccept" ],
    "RefuseDuelCommand": [ "duelrefuse" ]
  },
  "ConfigVersion": 1
}
```
