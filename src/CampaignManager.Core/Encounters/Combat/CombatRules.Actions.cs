using CampaignManager.Core.Dice;

namespace CampaignManager.Core.Encounters;

public static partial class CombatRules
{
    /// <summary>
    /// Укрыться от огня (стр. 111): проверка Уклонения; успех — стрелкам по нему штрафная кость в этом раунде, а сам он
    /// теряет следующую атаку — этого раунда, если ещё не атаковал, иначе следующего. Провал — ничего.
    /// </summary>
    public static EncounterResolution TakeCover(EncounterState state, Guid participantId, D100Roll? roll, IDiceRoller dice, int? dodge = null)
    {
        var p = Require(state, participantId);
        var value = dodge ?? p.Stats.Dodge;
        var test = Test(roll, value, dice);
        List<string> lines = [RollText($"{p.Name} (Уклонение)", test.Roll, value, test.Level)];
        if (!test.Passed)
            return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} не успел укрыться от огня.", lines, []);

        var blocked = p.Combat.AttacksIn(state.Round) == 0 ? state.Round : state.Round + 1;
        return Resolution(EncounterLogKind.Action, p.Id,
            $"{p.Name} укрылся от огня: стрелкам по нему штрафная кость, сам теряет атаку в раунде {N(blocked)} (стр. 111).",
            lines, [Effect(EncounterEffectKind.Cover, p.Id, blocked, flag: true)]);
    }

    /// <summary>Прицелиться (стр. 111): бонусная кость следующему выстрелу; рана, движение или выстрел прицел снимают.</summary>
    public static EncounterResolution Aim(EncounterState state, Guid participantId)
    {
        var p = Require(state, participantId);
        return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} целится: бонусная кость следующему выстрелу (стр. 111).", [],
            [Effect(EncounterEffectKind.Aim, p.Id, flag: true)]);
    }

    /// <summary>Перезарядка: магазин полон (стр. 111).</summary>
    public static EncounterResolution Reload(EncounterState state, Guid participantId, string attackKey)
    {
        var p = Require(state, participantId);
        var attack = AttackOf(p, attackKey);
        if (attack.AmmoCapacity is not { } capacity)
            return Resolution(EncounterLogKind.Action, p.Id, $"{attack.Name}: магазина нет — перезаряжать нечего.", [], []);

        return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} перезаряжает {attack.Name}.", [],
            [new EncounterEffect { Kind = EncounterEffectKind.Ammo, ParticipantId = p.Id, Key = attack.Key, Amount = capacity }]);
    }

    /// <summary>
    /// Починка заклинившего оружия (стр. 113): попытка раз в раунд, нужен успех Механики или Стрельбы; всего — столько
    /// успешных раундов, сколько выпало на 1d6 при осечке.
    /// </summary>
    public static EncounterResolution RepairJam(EncounterState state, Guid participantId, int skill, D100Roll? roll, IDiceRoller dice)
    {
        var p = Require(state, participantId);
        if (p.Combat.JammedAttack is not { } key)
            return Resolution(EncounterLogKind.Action, p.Id, $"У {p.Name} ничего не заклинило.", [], []);

        var attack = AttackOf(p, key);
        var test = Test(roll, skill, dice);
        List<string> lines = [RollText($"{p.Name} (починка)", test.Roll, skill, test.Level)];
        if (!test.Passed)
            return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} не смог продвинуться в починке {attack.Name}.", lines, []);

        var left = Math.Max(0, p.Combat.JamRoundsLeft - 1);
        return Resolution(EncounterLogKind.Action, p.Id,
            left == 0 ? $"{p.Name} починил {attack.Name}." : $"{p.Name} чинит {attack.Name}: осталось {N(left)} р.",
            lines, [new EncounterEffect { Kind = EncounterEffectKind.Jam, ParticipantId = p.Id, Key = key, Amount = left }]);
    }

    /// <summary>Бегство из ближнего боя: участник выходит из очереди (вернуть — «Вернуть» в строке).</summary>
    public static EncounterResolution Flee(EncounterState state, Guid participantId)
    {
        var p = Require(state, participantId);
        return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} бежит из боя.", [],
            [Effect(EncounterEffectKind.Out, p.Id, flag: true, detail: "бегство")]);
    }

    /// <summary>Огнестрел наготове: +50 к ЛВК при очерёдности со следующего раунда (стр. 110).</summary>
    public static EncounterResolution FirearmReady(EncounterState state, Guid participantId, bool ready)
    {
        var p = Require(state, participantId);
        return Resolution(EncounterLogKind.Action, p.Id,
            ready ? $"{p.Name}: огнестрел наготове — +50 к инициативе (стр. 110)." : $"{p.Name}: огнестрел убран.", [],
            [Effect(EncounterEffectKind.Ready, p.Id, flag: ready)]);
    }

    /// <summary>Цена Удачи, чтобы остаться в сознании ещё на раунд: 1, 2, 4, 8… (необязательное правило, стр. 123).</summary>
    public static int LuckCostToStayConscious(int luckSpentSoFar)
    {
        var payments = 0;
        var total = 0;
        while (total < luckSpentSoFar)
        {
            total += 1 << payments;
            payments++;
        }

        return 1 << payments;
    }

    /// <summary>Остаться в сознании до конца раунда за Удачу (стр. 123). Null — правило выключено или Удачи не хватает.</summary>
    public static EncounterResolution? StayConscious(EncounterState state, Guid participantId)
    {
        var p = Require(state, participantId);
        var cost = LuckCostToStayConscious(p.Combat.LuckSpentToStayConscious);
        if (!state.Combat.LuckToStayConscious || p.Luck is not { } luck || luck < cost || p.Dead)
            return null;

        return Resolution(EncounterLogKind.Action, p.Id, $"{p.Name} тратит {N(cost)} Удачи и остаётся в сознании до конца раунда (стр. 123).", [],
            [Effect(EncounterEffectKind.Luck, p.Id, -cost), Effect(EncounterEffectKind.Awake, p.Id, flag: true)]);
    }

    /// <summary>
    /// Броски инициативы (необязательное правило, стр. 122): каждый проверяет ЛВК, огнестрел наготове — бонусная кость;
    /// порядок — по уровню успеха, при равенстве — по ЛВК; держится до конца боя. Критический успех — тактическое
    /// преимущество, крах — пропуск первого хода (решает Хранитель). Меняет документ сразу — это расстановка, а не эффект.
    /// </summary>
    public static void RollInitiative(EncounterState state, IReadOnlyDictionary<Guid, D100Roll> entered, IDiceRoller dice, DateTimeOffset now)
    {
        List<string> lines = [];
        foreach (var p in EncounterQueue.Order(state).Select(state.Find).OfType<EncounterParticipant>())
        {
            var roll = entered.TryGetValue(p.Id, out var r) ? r : D100.Roll(dice, p.Combat.FirearmReady ? 1 : 0);
            var level = Check.Evaluate(roll.Result, p.Stats.Dex);
            p.Combat.InitiativeRoll = roll.Result;
            p.Combat.InitiativeLevel = level;
            var note = level switch
            {
                SuccessLevel.Critical => " — тактическое преимущество",
                SuccessLevel.Fumble => " — пропускает первый ход",
                _ => "",
            };
            lines.Add(RollText($"{p.Name} (ЛВК)", roll, p.Stats.Dex, level) + note);
        }

        state.Combat.InitiativeRolls = true;
        state.Combat.InitiativeRolled = true;
        if (state.Round > 0)
            lines.Add("Новый порядок — со следующего раунда.");
        EncounterEngine.Log(state, new EncounterLogEntry
        {
            Kind = EncounterLogKind.Initiative, Text = "Броски инициативы: порядок по уровню успеха, затем по ЛВК (стр. 122).", Lines = lines, At = now,
        });
    }

    /// <summary>Вернуться к порядку по ЛВК.</summary>
    public static void ClearInitiative(EncounterState state, DateTimeOffset now)
    {
        foreach (var p in state.Participants)
        {
            p.Combat.InitiativeLevel = null;
            p.Combat.InitiativeRoll = null;
        }

        state.Combat.InitiativeRolled = false;
        EncounterEngine.Log(state, new EncounterLogEntry { Kind = EncounterLogKind.Initiative, Text = "Очерёдность снова по ЛВК.", At = now });
    }
}
