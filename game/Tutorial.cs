using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private TutorialLesson? _tutorial;
    private ContentCatalog? _contentBeforeTutorial;

    private void EndTutorial()
    {
        _tutorial = null; _orbitDragging = false;
        if (_contentBeforeTutorial == null) return;
        _content = _contentBeforeTutorial; _contentBeforeTutorial = null;
        _engine = new(_content);
    }

    private void StartTutorial()
    {
        StopNetwork();
        EndTutorial();
        _contentBeforeTutorial = _content;
        _content = _content.CopyForRuntime();
        _engine = new(_content);
        TutorialLesson.Register(_content);
        _tutorial = new();
        StartBattle(TutorialLesson.MapId, false, persist: false, preserveTutorial: true);
        _status = "Учебный бой: выполняй задания справа. Из обучения всегда можно выйти в меню.";
        DrawBattle();
    }

    private void TutorialCameraMoved()
    {
        if (_tutorial?.Step != TutorialStep.Camera) return;
        _tutorial.Continue();
        _status = "Камера поворачивается. Колёсико меняет масштаб, F возвращает исходный ракурс.";
        DrawBattle();
    }

    private void SelectUnit(string id)
    {
        if (_tutorial is { Step: < TutorialStep.Select }) return;
        if (_tutorial != null && id != TutorialLesson.ScoutId)
        {
            _status = "В этом задании командуем синими перехватчиками s0-0. Выбери их на поле или слева.";
            DrawBattle(); return;
        }
        _selected = id;
        _inspectedObject = null;
        _tutorial?.Select(id);
        DrawBattle();
    }

    private void SetBattleAction(string action)
    {
        _action = action; DrawBattle();
    }

    private void DrawTutorial(Control parent)
    {
        if (_tutorial == null) return;
        var (title, body) = _tutorial.Step switch
        {
            TutorialStep.Welcome => ("1 / 7   ЧТО НА ПОЛЕ", "Синие — твой флот, красные — противник. Серый объект нельзя пройти насквозь.\n\nГруппа кубиков — одно подразделение. ×6 означает шесть кораблей. Крупные кубики — фрегаты.\n\nЦель обычного боя — уничтожить флот противника. Здесь разберём управление за несколько действий."),
            TutorialStep.Camera => ("2 / 7   ОСМОТРИ ПОЛЕ", "Наведи курсор на поле. Зажми ПРАВУЮ кнопку мыши и подвигай мышью: поле можно осматривать с разных сторон.\n\nКолёсико — приблизить или отдалить.\nF — вернуть исходный ракурс.\n\nПоверни камеру, чтобы продолжить."),
            TutorialStep.Select => ("3 / 7   ВЫБЕРИ КОРАБЛИ", "Нажми ЛЕВОЙ кнопкой на синие перехватчики в золотом кольце. Можно выбрать их и в списке слева.\n\nНужное подразделение: s0-0, шесть маленьких кубиков.\n\nСправа находятся подсказки и кнопки приказов."),
            TutorialStep.Move => ("4 / 7   ПЕРЕМЕЩЕНИЕ", "Выбраны перехватчики s0-0.\n\nНажми «ПЕРЕМЕЩЕНИЕ», затем ЛЕВОЙ кнопкой нажми на золотую клетку «СЮДА». Это два шага по сетке.\n\nХод 3 означает запас движения в клетках. Перемещение — один приказ: после него подразделение темнеет и ждёт нового раунда."),
            TutorialStep.EndTurn => ("5 / 7   ПРИКАЗЫ И РАУНДЫ", "Корабли переместились и потемнели: свой приказ в этом раунде они уже получили.\n\nСтороны чередуют приказы. Учебный противник пропустил свои.\n\nНажми «ЗАВЕРШИТЬ ХОД», чтобы отказаться от оставшихся приказов. Начнётся новый раунд, и корабли снова смогут действовать."),
            TutorialStep.Attack => ("6 / 7   ОТКРОЙ ОГОНЬ", "Перехватчики s0-0 снова готовы.\n\nНажми «АТАКА», затем ЛЕВОЙ кнопкой нажми на красные кубики в золотом кольце. Они в соседней клетке — в пределах дальности 1.\n\nБроски определяют попадания, броня снижает потери. Даже промах расходует приказ. Движение и атака — разные приказы."),
            _ => ("7 / 7   ОБУЧЕНИЕ ПРОЙДЕНО", "Ты выбрал подразделение, переместил его, завершил ход и атаковал. Результат стрельбы — внизу.\n\nВ бою: выбери готовый синий отряд → выбери приказ → нажми клетку или врага.\n\nПКМ — обзор · колёсико — масштаб · F — исходный вид.\n\nМожно начать скирмиш с небольшими флотами и без объектов.")
        };
        Text(parent, title, 17, _colors.Highlight);
        Text(parent, body, 16);
        if (_tutorial.Step == TutorialStep.Welcome)
            Button(parent, "НАЧАТЬ ОБУЧЕНИЕ", () => { _tutorial.Continue(); DrawBattle(); });
        if (_tutorial.Step == TutorialStep.Camera)
            Button(parent, "ПРОПУСТИТЬ ОБЗОР", () => { _tutorial.Continue(); DrawBattle(); });
        if (_tutorial.Step == TutorialStep.Complete)
        {
            Button(parent, "СКИРМИШ / ГЕНЕРАТОР", () => { EndTutorial(); _state = null; GeneratorMenu(); });
            Button(parent, "ПРОЙТИ ЕЩЁ РАЗ", StartTutorial);
        }
    }

    private bool ApplyTutorialCommand(BattleCommand command)
    {
        if (_tutorial == null) return false;
        if (!_tutorial.Allows(command))
        {
            _status = "Выполни текущее задание справа: нужная цель выделена золотым.";
            DrawBattle(); return true;
        }
        var result = _engine.Apply(_state!, 0, command);
        if (!result.Accepted) { _status = result.Message; DrawBattle(); return true; }
        _state = result.State;
        _tutorial.Accepted(command);
        if (command.Kind == "move") _status = "Перехватчики переместились. Учебный противник пропускает приказы.";
        if (command.Kind == "pass")
        {
            _selected = TutorialLesson.ScoutId;
            _status = "Новый раунд: подразделения снова готовы. Перехватчики уже выбраны — попробуй атаку.";
        }
        if (command.Kind == "attack")
        {
            int remaining = _state.Units.FirstOrDefault(u => u.Id == TutorialLesson.TargetId)?.Models ?? 0;
            _status = $"Залп выполнен. Потери противника: {6 - remaining}, осталось: {remaining}. Приказ потрачен. Обучение пройдено!";
        }
        // A passive training opponent keeps the real alternating-order/round rules intact.
        if (_state.ActiveSide == 1 && !_state.Finished) _state = _engine.Apply(_state, 1, new("pass")).State;
        DrawBattle(); return true;
    }

    private void DrawTutorialMarker()
    {
        if (_tutorial == null) return;
        Hex? cell = _tutorial.Step switch
        {
            TutorialStep.Select => new(1, 2),
            TutorialStep.Move => TutorialLesson.Destination,
            TutorialStep.Attack => new(4, 2),
            _ => null
        };
        if (cell == null) return;
        _world.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.88f, OuterRadius = 1, Rings = 6, RingSegments = 36 },
            Position = World(cell.Value) + Vector3.Up * 0.08f,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = C(_colors.Highlight), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        if (_tutorial.Step == TutorialStep.Move)
            _world.AddChild(new Label3D { Text = "СЮДА", Position = World(cell.Value) + Vector3.Up * 0.9f, FontSize = 34,
                PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = C(_colors.Highlight) });
    }
}
