using Autodesk.Revit.DB;

namespace Projects.Utils
{
    /// <summary>
    /// Геометрические координаты точки во внутренних единицах Revit (футы).
    /// decimal даёт детерминированную десятичную арифметику без погрешностей double.
    /// </summary>
    public struct Coordinate
    {
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }

        public Coordinate(decimal x, decimal y, decimal z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Расстояние до другой точки (во внутренних единицах).
        /// decimal не имеет Math.Sqrt, поэтому только для извлечения корня
        /// промежуточно кастуем в double — результат возвращается как decimal.
        /// </summary>
        public decimal DistanceTo(Coordinate other)
        {
            decimal dx = X - other.X;
            decimal dy = Y - other.Y;
            decimal dz = Z - other.Z;
            double sumSq = (double)(dx * dx + dy * dy + dz * dz);
            return (decimal)System.Math.Sqrt(sumSq);
        }

        public override string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
    }

    /// <summary>
    /// Числовые / геометрические данные трубы.
    /// Все координаты и размеры — decimal, во внутренних единицах Revit (футы).
    /// Не содержит ни одного ссылочного типа Revit API.
    /// </summary>
    public struct myPipe_numeric
    {
        /// <summary>ElementId.IntegerValue трубы</summary>
        public int Id { get; set; }

        /// <summary>Номинальный диаметр во внутренних единицах (футы)</summary>
        public decimal Diameter { get; set; }

        /// <summary>Коннектор с индексом 0</summary>
        public Coordinate connector_0 { get; set; }

        /// <summary>Коннектор с индексом 1</summary>
        public Coordinate connector_1 { get; set; }
    }

    /// <summary>
    /// Категориальные данные трубы.
    /// Содержит ElementId — используется только на входе (выбор) и выходе (Pipe.Create).
    /// </summary>
    public struct myPipe_categoric
    {
        public ElementId pipe_Id { get; set; }
        public ElementId type_Id { get; set; }
        public ElementId level_Id { get; set; }
        public ElementId system_type_Id { get; set; }
    }

    /// <summary>
    /// Полные данные одной трубы: числовые + категориальные.
    /// Возвращается из SelectionHelper вместо Revit Pipe.
    /// </summary>
    public struct PipeData
    {
        public myPipe_numeric Numeric { get; set; }
        public myPipe_categoric Categoric { get; set; }
    }
}