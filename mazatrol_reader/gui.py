"""wxPython GUI for Mazatrol program viewing and editing."""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Any

import wx
import wx.lib.mixins.listctrl as listmix

from mazatrol_reader.config import (
    BACKGROUND_IMAGE,
    DEFAULT_PROGRAM,
    PROGRAMS_DIR,
    SUPPORTED_PROGRAM_EXTENSIONS,
)
from mazatrol_reader.editor import ProgramEditor
from mazatrol_reader.models import ParameterType, ProgramBlock, UnitEditAction
from mazatrol_reader.parser import PBGParser, TurningProfileExtractor
from mazatrol_reader.visualization import DisplayController, OCC_AVAILABLE, TurningSimulator

logger = logging.getLogger(__name__)


class EditableListCtrl(wx.ListCtrl, listmix.TextEditMixin):
    """List control with in-place text editing for parameter values."""

    def __init__(
        self,
        parent: wx.Window,
        *,
        style: int = wx.LC_REPORT | wx.LC_SINGLE_SEL,
    ) -> None:
        super().__init__(parent, style=style)
        listmix.TextEditMixin.__init__(self)


class ProgramContextMenu(wx.Menu):
    """Right-click menu for unit-level edit operations."""

    def __init__(self, callback: Any) -> None:
        super().__init__()
        self._callback = callback

        self.Append(self._bind("Delete Unit", UnitEditAction.DELETE))
        self.Append(self._bind("Duplicate Unit", UnitEditAction.DUPLICATE))
        self.Append(self._bind("Export Unit", UnitEditAction.EXPORT))

        insert_menu = wx.Menu()
        insert_menu.Append(self._bind("LIN", UnitEditAction.INSERT_LIN))
        insert_menu.Append(self._bind("TPR", UnitEditAction.INSERT_TPR))
        insert_menu.Append(self._bind("FACING", UnitEditAction.INSERT_FACING))
        self.AppendSubMenu(insert_menu, "Insert Unit")

    def _bind(self, label: str, action: UnitEditAction) -> int:
        item_id = wx.NewIdRef()
        item = wx.MenuItem(self, item_id, label)
        self.Append(item)
        self.Bind(wx.EVT_MENU, lambda evt, a=action: self._callback(a), item)
        return item_id


class ErrorDialog(wx.Dialog):
    def __init__(self, parent: wx.Window, message: str) -> None:
        super().__init__(parent, title="Error", size=(420, 140))
        panel = wx.Panel(self)
        sizer = wx.BoxSizer(wx.VERTICAL)
        sizer.Add(
            wx.StaticText(panel, label=message),
            1,
            wx.ALL | wx.EXPAND,
            12,
        )
        btn = wx.Button(panel, wx.ID_OK, "OK")
        sizer.Add(btn, 0, wx.ALIGN_RIGHT | wx.ALL, 8)
        panel.SetSizer(sizer)
        self.CenterOnParent()


class ProgramFileDropTarget(wx.FileDropTarget):
    """Accept Mazatrol program files via drag and drop."""

    def __init__(self, panel: ProgramPanel) -> None:
        super().__init__()
        self.panel = panel

    def OnDropFiles(self, _x: int, _y: int, filenames: list[str]) -> bool:
        return self.panel.handle_drop_files(filenames)


class ProgramFileDropTarget(wx.FileDropTarget):
    """Accept Mazatrol program files via drag and drop."""

    def __init__(self, panel: "ProgramPanel") -> None:
        super().__init__()
        self.panel = panel

    def OnDropFiles(self, _x: int, _y: int, filenames: list[str]) -> bool:
        return self.panel.handle_drop_files(filenames)


class ProgramPanel(wx.Panel):
    """Program list, file controls, and editing hooks."""

    def __init__(
        self,
        parent: wx.Window,
        parser: PBGParser,
        editor: ProgramEditor,
        on_simulate: Any,
    ) -> None:
        super().__init__(parent)
        self.parser = parser
        self.editor = editor
        self.on_simulate = on_simulate

        self.file_path: Path | None = None
        self.blocks: list[ProgramBlock] = []
        self.list_meta: list[list[list[Any]]] = []

        self._build_ui()
        self._bind_events()

        if DEFAULT_PROGRAM.is_file():
            self.load_program(DEFAULT_PROGRAM)
        elif PROGRAMS_DIR.is_dir():
            candidates = sorted(
                p for p in PROGRAMS_DIR.iterdir() if p.suffix.lower() in SUPPORTED_PROGRAM_EXTENSIONS
            )
            if candidates:
                self.load_program(candidates[0])

    def _build_ui(self) -> None:
        toolbar = wx.BoxSizer(wx.HORIZONTAL)

        refresh_btn = wx.Button(self, label="Refresh")
        simulate_btn = wx.Button(self, label="Play")
        open_btn = wx.Button(self, label="Open…")

        self.file_combo = wx.ComboBox(self, style=wx.CB_READONLY)
        self._populate_sample_files()

        toolbar.Add(refresh_btn, 0, wx.ALL, 4)
        toolbar.Add(simulate_btn, 0, wx.ALL, 4)
        toolbar.Add(open_btn, 0, wx.ALL, 4)
        toolbar.Add(self.file_combo, 1, wx.ALL | wx.EXPAND, 4)

        self.list_ctrl = EditableListCtrl(self)
        self.list_ctrl.SetBackgroundColour(wx.Colour(0, 0, 0))
        for col in range(1, 21):
            self.list_ctrl.InsertColumn(col, "")
            self.list_ctrl.SetColumnWidth(col, 72)

        root = wx.BoxSizer(wx.VERTICAL)
        root.Add(toolbar, 0, wx.EXPAND)
        root.Add(self.list_ctrl, 1, wx.EXPAND | wx.ALL, 4)
        self.SetSizer(root)

        self.refresh_btn = refresh_btn
        self.simulate_btn = simulate_btn
        self.open_btn = open_btn

    def _bind_events(self) -> None:
        self.refresh_btn.Bind(wx.EVT_BUTTON, self.on_refresh)
        self.simulate_btn.Bind(wx.EVT_BUTTON, self.on_simulate_clicked)
        self.open_btn.Bind(wx.EVT_BUTTON, self.on_open_file)
        self.file_combo.Bind(wx.EVT_COMBOBOX, self.on_sample_selected)
        self.list_ctrl.Bind(wx.EVT_LIST_END_LABEL_EDIT, self.on_cell_edited)
        self.list_ctrl.Bind(wx.EVT_LIST_ITEM_RIGHT_CLICK, self.on_right_click)

        drop = ProgramFileDropTarget(self)
        self.SetDropTarget(drop)
        self.list_ctrl.SetDropTarget(ProgramFileDropTarget(self))

    def _populate_sample_files(self) -> None:
        names: list[str] = []
        if PROGRAMS_DIR.is_dir():
            names = sorted(
                p.name
                for p in PROGRAMS_DIR.iterdir()
                if p.suffix.lower() in SUPPORTED_PROGRAM_EXTENSIONS
            )
        self.file_combo.Set(names)

    def handle_drop_files(self, filenames: list[str]) -> bool:
        for name in filenames:
            path = Path(name)
            if path.suffix.lower() in SUPPORTED_PROGRAM_EXTENSIONS:
                self.load_program(path)
                return True
        wx.MessageBox(
            "Drop a supported Mazatrol program file "
            f"({', '.join(sorted(SUPPORTED_PROGRAM_EXTENSIONS))}).",
            "Unsupported file",
            wx.OK | wx.ICON_WARNING,
            parent=self,
        )
        return False

    def on_open_file(self, _event: wx.CommandEvent) -> None:
        wildcard = "|".join(
            f"{ext.upper()} files (*{ext})|*{ext}" for ext in sorted(SUPPORTED_PROGRAM_EXTENSIONS)
        )
        wildcard += "|All files (*.*)|*.*"

        with wx.FileDialog(
            self,
            "Open Mazatrol program",
            wildcard=wildcard,
            style=wx.FD_OPEN | wx.FD_FILE_MUST_EXIST,
        ) as dialog:
            if dialog.ShowModal() == wx.ID_OK:
                self.load_program(Path(dialog.GetPath()))

    def on_sample_selected(self, event: wx.CommandEvent) -> None:
        name = self.file_combo.GetStringSelection()
        if name:
            self.load_program(PROGRAMS_DIR / name)

    def on_refresh(self, _event: wx.CommandEvent) -> None:
        if self.file_path:
            self.load_program(self.file_path)

    def on_simulate_clicked(self, _event: wx.CommandEvent) -> None:
        if not self.blocks:
            wx.MessageBox("Load a program first.", "Simulation", wx.OK | wx.ICON_INFORMATION, parent=self)
            return
        try:
            profile = TurningProfileExtractor.extract(self.blocks)
            self.on_simulate(profile)
        except Exception as exc:  # noqa: BLE001 - surface user-facing errors
            logger.exception("Simulation failed")
            wx.MessageBox(str(exc), "Simulation error", wx.OK | wx.ICON_ERROR, parent=self)

    def load_program(self, file_path: Path | str) -> None:
        path = Path(file_path)
        try:
            self.blocks = self.parser.parse(path)
            self.file_path = path
            self._render_blocks()
            logger.info("Loaded program %s", path)
        except Exception as exc:  # noqa: BLE001
            logger.exception("Failed to load %s", path)
            wx.MessageBox(f"Could not load program:\n{exc}", "Load error", wx.OK | wx.ICON_ERROR, parent=self)

    def _render_blocks(self) -> None:
        self.list_ctrl.DeleteAllItems()
        self.list_ctrl.DeleteAllColumns()
        for col in range(1, 21):
            self.list_ctrl.InsertColumn(col, "")
            self.list_ctrl.SetColumnWidth(col, 72)

        self.list_meta = []
        row_index = 0
        last_command = ""

        for block in self.blocks:
            legacy_rows = block.to_legacy_rows()
            info_title: list[list[Any]] = []
            info_data: list[list[Any]] = []

            if legacy_rows[0][0] == "UNo":
                row_index += 1
                self.list_ctrl.InsertItem(row_index, str(row_index))
                self.list_meta.append([])

            show_title = legacy_rows[0][0] == "UNo" or legacy_rows[0][0] != last_command
            if show_title:
                row_index += 1
                title_row = self.list_ctrl.InsertItem(row_index, str(row_index))
                self.list_ctrl.SetItemTextColour(title_row, wx.Colour(0, 200, 0))
                info_title.append([row_index, 0, row_index, 0, ""])

            last_command = legacy_rows[0][0]
            row_index += 1
            data_row = self.list_ctrl.InsertItem(row_index, str(row_index))
            self.list_ctrl.SetItemTextColour(data_row, wx.Colour(220, 220, 0))

            for col_idx, param in enumerate(legacy_rows, start=1):
                if show_title:
                    self.list_ctrl.SetItem(title_row, col_idx, str(param[0]))
                    info_title.append([row_index, col_idx, str(param[0]), param[2], param[3]])
                self.list_ctrl.SetItem(data_row, col_idx, str(param[1]))
                info_data.append([row_index, col_idx, str(param[1]), param[2], param[3]])

            if show_title:
                self.list_meta.append(info_title)
            self.list_meta.append(info_data)

    def on_cell_edited(self, event: wx.ListEvent) -> None:
        if not self.file_path:
            event.Skip()
            return

        row = event.GetIndex()
        col = event.GetColumn()
        if row >= len(self.list_meta) or col >= len(self.list_meta[row]):
            event.Skip()
            return

        meta = self.list_meta[row][col]
        param_type = meta[4]
        if param_type != ParameterType.READ_DATA.value:
            ErrorDialog(self, "This value cannot be changed.").ShowModal()
            if self.file_path:
                self.load_program(self.file_path)
            return

        try:
            self.parser.write_parameter(
                self.file_path,
                int(meta[3]),
                param_type,
                event.GetText(),
            )
            self.load_program(self.file_path)
        except Exception as exc:  # noqa: BLE001
            logger.exception("Parameter update failed")
            wx.MessageBox(str(exc), "Edit error", wx.OK | wx.ICON_ERROR, parent=self)

    @staticmethod
    def _meta_value(
        row_meta: list[list[Any]],
        *,
        column: int,
        field: int,
        default: Any = 0,
    ) -> Any:
        for cell in row_meta:
            if cell[1] == column:
                return cell[field]
        return default

    def on_right_click(self, event: wx.ListEvent) -> None:
        if not self.file_path:
            return

        line_number = int(event.GetText())
        if line_number <= 0 or line_number > len(self.list_meta):
            return

        header_row = self.list_meta[line_number - 1]
        unit_address = self._meta_value(header_row, column=18, field=3)
        unit_name = self._meta_value(header_row, column=2, field=2, default="UNIT")

        pending_action: list[UnitEditAction | None] = [None]

        def choose(action: UnitEditAction) -> None:
            pending_action[0] = action

        menu = ProgramContextMenu(choose)
        self.PopupMenu(menu)

        action = pending_action[0]
        if action is None:
            return

        try:
            self.editor.apply(self.file_path, int(unit_address), action, str(unit_name))
            self.load_program(self.file_path)
        except Exception as exc:  # noqa: BLE001
            logger.exception("Unit edit failed")
            wx.MessageBox(str(exc), "Edit error", wx.OK | wx.ICON_ERROR, parent=self)


class MainFrame(wx.Frame):
    """Main application window with program list and 3D viewer."""

    def __init__(self) -> None:
        super().__init__(
            None,
            title="Mazatrol Reader — Mazak Program Viewer",
            size=(1400, 900),
        )

        if not OCC_AVAILABLE:
            wx.MessageBox(
                "pythonOCC is not installed.\n"
                "Program parsing will work, but 3D simulation is disabled.\n\n"
                "Install with: conda install -c conda-forge pythonocc-core",
                "3D viewer unavailable",
                wx.OK | wx.ICON_WARNING,
            )

        self.parser = PBGParser()
        self.editor = ProgramEditor()
        self.display = None
        self.display_controller: DisplayController | None = None

        self._build_layout()
        self.Centre()

    def _build_layout(self) -> None:
        splitter = wx.SplitterWindow(self, style=wx.SP_LIVE_UPDATE)
        left = wx.Panel(splitter)
        right = wx.Panel(splitter)

        self.program_panel = ProgramPanel(
            left,
            parser=self.parser,
            editor=self.editor,
            on_simulate=self.run_simulation,
        )
        left_sizer = wx.BoxSizer(wx.VERTICAL)
        left_sizer.Add(self.program_panel, 1, wx.EXPAND)
        left.SetSizer(left_sizer)

        right_sizer = wx.BoxSizer(wx.VERTICAL)
        if OCC_AVAILABLE:
            from OCC.Display.wxDisplay import wxViewer3d

            self.viewer = wxViewer3d(right, -1)
            right_sizer.Add(self.viewer, 1, wx.EXPAND)

            view_bar = wx.BoxSizer(wx.HORIZONTAL)
            for label, handler in (
                ("ISO", self._view_iso),
                ("Front", self._view_front),
                ("Side", self._view_side),
                ("Up", self._view_top),
            ):
                btn = wx.Button(right, label=label)
                btn.Bind(wx.EVT_BUTTON, handler)
                view_bar.Add(btn, 0, wx.ALL, 4)
            right_sizer.Add(view_bar, 0, wx.EXPAND)
        else:
            self.viewer = wx.StaticText(
                right,
                label="Install pythonocc-core to enable 3D simulation.",
            )
            right_sizer.Add(self.viewer, 1, wx.ALIGN_CENTER)

        right.SetSizer(right_sizer)
        splitter.SplitVertically(left, right, sashPosition=1000)
        splitter.SetMinimumPaneSize(320)

        frame_sizer = wx.BoxSizer(wx.VERTICAL)
        frame_sizer.Add(splitter, 1, wx.EXPAND)
        self.SetSizer(frame_sizer)

    def initialize_viewer(self) -> None:
        if not OCC_AVAILABLE:
            return
        self.viewer.InitDriver()
        self.display = self.viewer._display
        self.display_controller = DisplayController(self.display)
        self.display_controller.configure_scene()
        if BACKGROUND_IMAGE.is_file():
            self.display_controller.set_background_image(str(BACKGROUND_IMAGE))

    def run_simulation(self, profile: Any) -> None:
        if not self.display_controller:
            wx.MessageBox(
                "3D viewer is not available.",
                "Simulation",
                wx.OK | wx.ICON_WARNING,
                parent=self,
            )
            return
        try:
            shape = TurningSimulator(profile).build_shape()
            self.display_controller.show_shape(shape)
        except Exception as exc:  # noqa: BLE001
            logger.exception("3D build failed")
            wx.MessageBox(str(exc), "Simulation error", wx.OK | wx.ICON_ERROR, parent=self)

    def _view_iso(self, _event: wx.CommandEvent) -> None:
        if self.display_controller:
            self.display_controller.view_iso()

    def _view_front(self, _event: wx.CommandEvent) -> None:
        if self.display_controller:
            self.display_controller.view_front()

    def _view_side(self, _event: wx.CommandEvent) -> None:
        if self.display_controller:
            self.display_controller.view_side()

    def _view_top(self, _event: wx.CommandEvent) -> None:
        if self.display_controller:
            self.display_controller.view_top()


class MazatrolApp(wx.App):
    def OnInit(self) -> bool:
        wx.InitAllImageHandlers()
        self.frame = MainFrame()
        self.frame.Show()
        wx.CallAfter(self.frame.initialize_viewer)
        self.SetTopWindow(self.frame)
        return True
