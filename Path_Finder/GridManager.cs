using Autodesk.Revit.DB;
using System;

namespace MEP_Data.Path_Finder
{
    public class GridManager
    {
        public Node[,,] Grid { get; private set; }

        private readonly double _step;
        private readonly double _minX, _minY, _minZ;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Depth { get; private set; }

        /// <summary>
        /// Создаёт 3D-сетку узлов между двумя точками мирового пространства.
        /// </summary>
        /// <param name="x1, y1, z1">Координаты первой точки (внутренние единицы Revit — футы)</param>
        /// <param name="x2, y2, z2">Координаты второй точки (внутренние единицы Revit — футы)</param>
        /// <param name="step">Шаг сетки в миллиметрах (по умолчанию 100 мм)</param>
        public GridManager(
            double x1, double y1, double z1,
            double x2, double y2, double z2,
            double step = 100)
        {
            _step = UnitUtils.ConvertToInternalUnits(step, UnitTypeId.Millimeters);

            // Границы по каждой оси
            _minX = Math.Min(x1, x2);
            _minY = Math.Min(y1, y2);
            _minZ = Math.Min(z1, z2);

            double maxX = Math.Max(x1, x2);
            double maxY = Math.Max(y1, y2);
            double maxZ = Math.Max(z1, z2);

            // Размерность массива по каждой оси
            // +1 чтобы обе граничные точки попали в узлы сетки
            Width = (int)Math.Ceiling((maxX - _minX) / _step) + 1;
            Height = (int)Math.Ceiling((maxY - _minY) / _step) + 1;
            Depth = (int)Math.Ceiling((maxZ - _minZ) / _step) + 1;

            Grid = new Node[Width, Height, Depth];

            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    for (int z = 0; z < Depth; z++)
                    {
                        Grid[x, y, z] = new Node(x, y, z);
                    }
        }

        /// <summary>
        /// Возвращает узел сетки по мировым координатам (внутренние единицы Revit).
        /// Координаты за пределами сетки зажимаются до ближайшей границы.
        /// </summary>
        public Node GetNodeFromWorld(double worldX, double worldY, double worldZ)
        {
            int x = (int)Math.Round((worldX - _minX) / _step);
            int y = (int)Math.Round((worldY - _minY) / _step);
            int z = (int)Math.Round((worldZ - _minZ) / _step);

            x = Math.Max(0, Math.Min(x, Width - 1));
            y = Math.Max(0, Math.Min(y, Height - 1));
            z = Math.Max(0, Math.Min(z, Depth - 1));

            return Grid[x, y, z];
        }

        /// <summary>
        /// Конвертирует индексы узла обратно в мировые координаты (внутренние единицы Revit).
        /// Удобно для построения пути после A*.
        /// </summary>
        public (double worldX, double worldY, double worldZ) GetWorldFromNode(Node node)
        {
            return (
                _minX + node.X * _step,
                _minY + node.Y * _step,
                _minZ + node.Z * _step
            );
        }
    }
}