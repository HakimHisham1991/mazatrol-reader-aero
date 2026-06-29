"""3D turning simulation using pythonOCC (OpenCascade)."""

from __future__ import annotations

import logging
import math
from typing import TYPE_CHECKING, Any

from mazatrol_reader.models import BarFigure, MaterialStock, TurningSimulationInput

logger = logging.getLogger(__name__)

if TYPE_CHECKING:
    from OCC.Core.TopoDS import TopoDS_Shape

try:
    from OCC.Core.BRepAlgoAPI import BRepAlgoAPI_Cut
    from OCC.Core.BRepPrimAPI import (
        BRepPrimAPI_MakeCone,
        BRepPrimAPI_MakeCylinder,
        BRepPrimAPI_MakeTorus,
    )
    from OCC.Core.gp import gp_Ax2, gp_Dir, gp_Pnt, gp_XYZ
    from OCC.Core.Quantity import Quantity_Color, Quantity_TOC_RGB
    from OCC.Core.V3d import V3d_Xpos, V3d_Yneg, V3d_Zpos

    OCC_AVAILABLE = True
except ImportError:  # pragma: no cover - optional dependency at runtime
    OCC_AVAILABLE = False
    logger.warning("pythonOCC not installed; 3D simulation will be unavailable")


class TurningSimulator:
    """Build a turned part solid by boolean subtraction from cylindrical stock."""

    def __init__(self, simulation: TurningSimulationInput) -> None:
        if not OCC_AVAILABLE:
            raise RuntimeError(
                "pythonOCC is required for 3D simulation. "
                "Install pythonocc-core (conda-forge recommended on Windows)."
            )
        self.simulation = simulation

    def build_shape(self) -> TopoDS_Shape:
        stock = self.simulation.stock
        if stock is None:
            raise ValueError("Program has no MAT (material) unit; cannot build stock")

        shape = self._make_stock(stock)
        shape = self._apply_facing(shape, stock, self.simulation.facing)
        for figure in self.simulation.bar_figures:
            removed = self._build_removed_solid(figure, stock.od)
            shape = BRepAlgoAPI_Cut(shape, removed).Shape()

        logger.info(
            "Built turned part: OD=%.3f L=%.3f with %d bar figures",
            stock.od,
            stock.length,
            len(self.simulation.bar_figures),
        )
        return shape

    @staticmethod
    def _make_stock(stock: MaterialStock) -> TopoDS_Shape:
        axis = gp_Ax2(gp_Pnt(gp_XYZ(-stock.length, 0, 0)), gp_Dir(1, 0, 0))
        outer = BRepPrimAPI_MakeCylinder(axis, stock.od / 2, stock.length + stock.workface).Shape()
        if stock.inner_diameter <= 0:
            return outer
        bore = BRepPrimAPI_MakeCylinder(axis, stock.inner_diameter / 2, stock.length + stock.workface).Shape()
        return BRepAlgoAPI_Cut(outer, bore).Shape()

    @staticmethod
    def _apply_facing(
        shape: TopoDS_Shape,
        stock: MaterialStock,
        facing: Any | None,
    ) -> TopoDS_Shape:
        if facing is None or facing.finish_x <= 0:
            return shape

        axis = gp_Ax2(
            gp_Pnt(gp_XYZ(stock.workface - facing.finish_z, 0, 0)),
            gp_Dir(1, 0, 0),
        )
        face_cut = BRepPrimAPI_MakeCylinder(axis, facing.finish_x / 2, facing.finish_z).Shape()
        return BRepAlgoAPI_Cut(shape, face_cut).Shape()

    def _build_removed_solid(self, figure: BarFigure, stock_od: float) -> TopoDS_Shape:
        length = figure.finish_z - figure.start_z
        start_corner = figure.start_corner
        finish_corner = figure.finish_corner
        cut_length = length - start_corner - finish_corner

        solids: list[TopoDS_Shape] = []

        if start_corner:
            axis = gp_Ax2(gp_Pnt(gp_XYZ(-figure.start_z - start_corner, 0, 0)), gp_Dir(1, 0, 0))
            torus = BRepPrimAPI_MakeTorus(
                axis,
                figure.start_x / 2 - start_corner,
                start_corner,
                0,
                math.pi / 2,
            ).Shape()
            cylinder = BRepPrimAPI_MakeCylinder(axis, stock_od / 2, start_corner).Shape()
            solids.append(BRepAlgoAPI_Cut(cylinder, torus).Shape())

        if finish_corner:
            axis = gp_Ax2(gp_Pnt(gp_XYZ(-figure.finish_z + finish_corner, 0, 0)), gp_Dir(1, 0, 0))
            torus = BRepPrimAPI_MakeTorus(
                axis,
                figure.start_x / 2 - finish_corner,
                finish_corner,
                -math.pi / 2,
                0,
            ).Shape()
            cylinder = BRepPrimAPI_MakeCylinder(axis, stock_od / 2, finish_corner).Shape()
            solids.append(BRepAlgoAPI_Cut(cylinder, torus).Shape())

        main_axis = gp_Ax2(gp_Pnt(gp_XYZ(-figure.finish_z + finish_corner, 0, 0)), gp_Dir(1, 0, 0))
        envelope = BRepPrimAPI_MakeCylinder(main_axis, stock_od / 2, cut_length).Shape()

        if figure.start_x == figure.finish_x:
            profile = BRepPrimAPI_MakeCylinder(main_axis, figure.start_x / 2, cut_length).Shape()
        else:
            profile = BRepPrimAPI_MakeCone(
                main_axis,
                figure.finish_x / 2,
                figure.start_x / 2,
                cut_length,
            ).Shape()

        solids.append(BRepAlgoAPI_Cut(envelope, profile).Shape())

        result = solids[0]
        for solid in solids[1:]:
            result = BRepAlgoAPI_Cut(result, solid).Shape()
        return result


class DisplayController:
    """Configure pythonOCC wx viewer lighting, materials, and camera presets."""

    def __init__(self, display: Any) -> None:
        self.display = display

    @staticmethod
    def _part_color() -> Any:
        return Quantity_Color(0.72, 0.76, 0.82, Quantity_TOC_RGB)

    def configure_scene(self) -> None:
        """Enable basic lighting/shading for clearer part visualization."""
        try:
            self.display.EnableAntiAliasing()
        except Exception:  # noqa: BLE001 - viewer API varies slightly by version
            logger.debug("Anti-aliasing not available on this viewer")

        try:
            self.display.set_bg_gradient_color(
                (220, 220, 230),
                (180, 190, 210),
                2,
                update=True,
            )
        except Exception:  # noqa: BLE001
            logger.debug("Gradient background not available; using default")

        try:
            self.display.View.SetLightOn()
        except Exception:  # noqa: BLE001
            logger.debug("Could not enable default light")

    def set_background_image(self, image_path: str | None) -> None:
        if not image_path:
            return
        try:
            self.display.SetBackgroundImage(image_path)
        except Exception:  # noqa: BLE001
            logger.warning("Background image unavailable: %s", image_path)

    def show_shape(self, shape: TopoDS_Shape, *, update: bool = True) -> None:
        self.display.EraseAll()
        self.display.DisplayShape(
            shape,
            color=self._part_color(),
            update=update,
        )
        self.display.FitAll()
        self.display.Repaint()

    def view_iso(self) -> None:
        self.display.View_Iso()
        self.display.Repaint()

    def view_front(self) -> None:
        self.display.View.SetProj(V3d_Xpos)
        self.display.Repaint()

    def view_side(self) -> None:
        self.display.View.SetProj(V3d_Yneg)
        self.display.Repaint()

    def view_top(self) -> None:
        self.display.View.SetProj(V3d_Zpos)
        self.display.Repaint()
