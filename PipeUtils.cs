using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System.Collections.Generic;
using System.Linq;

namespace Projects.Utils
{
    public static class PipeUtils
    {
        // ════════════════════════════════════════════════════════════════════════════
        // ГРАНИЦА ВХОДА: Revit API (double) → Structs (decimal)
        // Единственное место, где читаем Pipe, Connector, XYZ.
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Конвертирует Revit Pipe в PipeData.
        /// После вызова Revit-объекты больше не нужны.
        /// </summary>
        public static PipeData ToPipeData(Pipe pipe)
        {
            var connectors = pipe.ConnectorManager.Connectors
                                 .Cast<Connector>()
                                 .ToList();

            Connector c0 = connectors.Count > 0 ? connectors[0] : null;
            Connector c1 = connectors.Count > 1 ? connectors[1] : null;

            // Диаметр читаем из параметра (double из Revit → decimal)
            double rawDiameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)
                                    ?.AsDouble() ?? 0.0;

            return new PipeData
            {
                Numeric = new myPipe_numeric
                {
                    Id = pipe.Id.IntegerValue,
                    Diameter = (decimal)rawDiameter,
                    connector_0 = c0 != null ? XYZToCoordinate(c0.Origin) : default,
                    connector_1 = c1 != null ? XYZToCoordinate(c1.Origin) : default,
                },
                Categoric = new myPipe_categoric
                {
                    pipe_Id = pipe.Id,
                    type_Id = pipe.GetTypeId(),
                    level_Id = GetLevelId(pipe),
                    system_type_Id = GetSystemTypeId(pipe),
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ГЕОМЕТРИЯ НА STRUCTS — только decimal, без Revit API
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Находит пару ближайших коннекторов между двумя трубами.
        /// Полностью на decimal — никакого Revit API.
        /// </summary>
        public static (Coordinate From, Coordinate To) GetClosestConnectors(
            myPipe_numeric p1, myPipe_numeric p2)
        {
            Coordinate bestFrom = p1.connector_0;
            Coordinate bestTo = p2.connector_0;
            decimal minDist = p1.connector_0.DistanceTo(p2.connector_0);

            decimal d;

            d = p1.connector_0.DistanceTo(p2.connector_1);
            if (d < minDist) { minDist = d; bestFrom = p1.connector_0; bestTo = p2.connector_1; }

            d = p1.connector_1.DistanceTo(p2.connector_0);
            if (d < minDist) { minDist = d; bestFrom = p1.connector_1; bestTo = p2.connector_0; }

            d = p1.connector_1.DistanceTo(p2.connector_1);
            if (d < minDist) { bestFrom = p1.connector_1; bestTo = p2.connector_1; }

            return (bestFrom, bestTo);
        }

        /// <summary>
        /// Расстояние в миллиметрах между двумя Coordinate.
        /// Конвертация из внутренних единиц через Revit UnitUtils.
        /// </summary>
        public static decimal DistanceMm(Coordinate a, Coordinate b)
        {
            // DistanceTo возвращает decimal; для UnitUtils нужен double
            double distInternal = (double)a.DistanceTo(b);
            double distMm = UnitUtils.ConvertFromInternalUnits(distInternal, UnitTypeId.Millimeters);
            return (decimal)distMm;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ГРАНИЦА ВЫХОДА: Structs (decimal) → Revit API (double)
        // Вызывается только перед Pipe.Create / Parameter.Set.
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>Coordinate (decimal) → XYZ (double) для передачи в Revit API.</summary>
        public static XYZ ToXYZ(Coordinate c)
            => new XYZ((double)c.X, (double)c.Y, (double)c.Z);

        /// <summary>decimal диаметр → double для Parameter.Set.</summary>
        public static double DiameterToDouble(decimal diameter)
            => (double)diameter;

        /// <summary>
        /// Находит Connector трубы, ближайший к заданной Coordinate.
        /// Используется на границе выхода для Pipe.Create(Connector, Connector) —
        /// этот overload физически соединяет трубы в одну сеть (Tab-выделение).
        /// </summary>
        public static Connector GetConnectorNear(Document doc, ElementId pipeId, Coordinate target)
        {
            Pipe pipe = doc.GetElement(pipeId) as Pipe;
            if (pipe == null) return null;

            Connector best = null;
            decimal minDist = decimal.MaxValue;

            foreach (Connector c in pipe.ConnectorManager.Connectors)
            {
                decimal d = XYZToCoordinate(c.Origin).DistanceTo(target);
                if (d < minDist)
                {
                    minDist = d;
                    best = c;
                }
            }

            return best;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // СОЕДИНЕНИЕ ТРУБ ОТВОДАМИ
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Соединяет список труб отводами (elbow) последовательно:
        ///   pipes[0] ↔ pipes[1] ↔ pipes[2] ↔ ... ↔ pipes[n]
        ///
        /// На каждом стыке метод:
        ///   1. Находит ближайшую пару коннекторов между соседними трубами.
        ///   2. Вызывает doc.Create.NewElbowFitting.
        ///
        /// Должен вызываться внутри открытой транзакции.
        /// Возвращает список созданных FamilyInstance (elbow-фитингов).
        /// </summary>
        /// <param name="doc">Документ Revit.</param>
        /// <param name="pipes">
        ///     Список труб в порядке соединения. Минимум 2 элемента.
        ///     Порядок важен — elbow ставится между pipes[i] и pipes[i+1].
        /// </param>
        public static List<FamilyInstance> ConnectWithElbows(Document doc, List<Pipe> pipes)
        {
            var result = new List<FamilyInstance>();

            if (pipes == null || pipes.Count < 2) return result;

            for (int i = 0; i < pipes.Count - 1; i++)
            {
                Pipe pipeA = pipes[i];
                Pipe pipeB = pipes[i + 1];

                if (pipeA == null || pipeB == null) continue;

                // Конвертируем оба конца обеих труб в Coordinate для поиска ближайших
                var connA = pipeA.ConnectorManager.Connectors.Cast<Connector>().ToList();
                var connB = pipeB.ConnectorManager.Connectors.Cast<Connector>().ToList();

                // Перебираем все пары, ищем ближайшую
                Connector bestA = null;
                Connector bestB = null;
                decimal minDist = decimal.MaxValue;

                foreach (Connector ca in connA)
                {
                    Coordinate coordA = XYZToCoordinate(ca.Origin);
                    foreach (Connector cb in connB)
                    {
                        decimal d = coordA.DistanceTo(XYZToCoordinate(cb.Origin));
                        if (d < minDist)
                        {
                            minDist = d;
                            bestA = ca;
                            bestB = cb;
                        }
                    }
                }

                if (bestA == null || bestB == null) continue;

                // Проверка 1: коннекторы уже соединены между собой
                bool alreadyConnected = false;
                foreach (Connector linked in bestA.AllRefs)
                {
                    if (linked.Owner.Id == bestB.Owner.Id)
                    {
                        alreadyConnected = true;
                        break;
                    }
                }
                if (alreadyConnected) continue;

                // Проверка 2: трубы коллинеарны — elbow между ними невозможен.
                // Revit бросает InvalidOperationException если |dot| ≈ 1.0
                Line lineA = (pipeA.Location as LocationCurve)?.Curve as Line;
                Line lineB = (pipeB.Location as LocationCurve)?.Curve as Line;
                if (lineA != null && lineB != null)
                {
                    double dot = System.Math.Abs(lineA.Direction.DotProduct(lineB.Direction));
                    if (dot > 1.0 - 1e-6) continue;  // коллинеарны — пропускаем
                }

                FamilyInstance elbow = doc.Create.NewElbowFitting(bestA, bestB);
                result.Add(elbow);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ПРОВЕРКА КОЛЛИНЕАРНОСТИ И СЛИЯНИЕ ТРУБ
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, является ли <paramref name="newPipe"/> прямым продолжением
        /// <paramref name="existingPipe"/> в точке <paramref name="junction"/>.
        ///
        /// Условия совпадения:
        ///   1. Конец existingPipe совпадает с началом newPipe (точка junction).
        ///   2. Направления обеих труб коллинеарны (dot product ≈ ±1.0).
        ///
        /// Если условия выполнены — продлевает existingPipe до дальнего конца newPipe,
        /// удаляет newPipe и возвращает true.
        /// Должен вызываться внутри открытой транзакции.
        /// </summary>
        /// <param name="doc">Документ Revit.</param>
        /// <param name="existingPipe">Исходная (старая) труба.</param>
        /// <param name="newPipe">Только что созданная труба.</param>
        /// <param name="junction">Точка стыка в координатах decimal.</param>
        /// <param name="angleTolerance">
        ///     Допуск на коллинеарность: максимальное отклонение |dot| от 1.0.
        ///     По умолчанию 1e-6 — практически идеально прямая линия.
        /// </param>
        public static bool TryMergeAsContinuation(
            Document doc,
            Pipe existingPipe,
            Pipe newPipe,
            Coordinate junction,
            double angleTolerance = 1e-6)
        {
            // ── 1. Получаем геометрию обеих труб как Line ───────────────────────────
            LocationCurve locExisting = existingPipe.Location as LocationCurve;
            LocationCurve locNew = newPipe.Location as LocationCurve;

            if (locExisting == null || locNew == null) return false;

            Line lineExisting = locExisting.Curve as Line;
            Line lineNew = locNew.Curve as Line;

            if (lineExisting == null || lineNew == null) return false;

            // ── 2. Проверяем коллинеарность через dot product ───────────────────────
            //
            //   dot =  1.0  → трубы сонаправлены   (→ →)
            //   dot = -1.0  → трубы противоположны  (← →) — тоже коллинеарны!
            //   |dot| < 1.0 → есть угол              → elbow нужен
            //
            XYZ dirExisting = lineExisting.Direction;          // единичный вектор Revit
            XYZ dirNew = lineNew.Direction;

            double dot = dirExisting.DotProduct(dirNew);

            if (System.Math.Abs(System.Math.Abs(dot) - 1.0) > angleTolerance)
                return false;   // трубы не коллинеарны — elbow нужен, выходим

            // ── 3. Находим дальний конец newPipe (противоположный junction) ─────────
            //
            //   newPipe: [junction] ──────────────────── [farEnd]
            //   Берём тот конец newPipe, который НЕ является точкой стыка.
            //
            XYZ junctionXYZ = ToXYZ(junction);

            XYZ p0 = lineNew.GetEndPoint(0);
            XYZ p1 = lineNew.GetEndPoint(1);

            // Дальний конец — тот, что дальше от junction
            XYZ farEnd = p0.DistanceTo(junctionXYZ) > p1.DistanceTo(junctionXYZ) ? p0 : p1;

            // ── 4. Находим какой конец existingPipe смотрит на junction ────────────
            //
            //   existingPipe: [origin] ────────────────── [junction]
            //   Нам нужно сдвинуть именно этот конец до farEnd.
            //
            XYZ ep0 = lineExisting.GetEndPoint(0);
            XYZ ep1 = lineExisting.GetEndPoint(1);

            // Индекс конца existingPipe, ближайшего к junction (0 или 1)
            int junctionEndIndex = ep0.DistanceTo(junctionXYZ) < ep1.DistanceTo(junctionXYZ)
                                   ? 0 : 1;

            // ── 5. Продлеваем existingPipe, удаляем newPipe ────────────────────────
            locExisting.Curve = Line.CreateBound(
                junctionEndIndex == 0 ? farEnd : ep0,   // новый endpoint 0
                junctionEndIndex == 0 ? ep1 : farEnd // новый endpoint 1
            );

            doc.Delete(newPipe.Id);

            return true;
        }

        // ════════════════════════════════════════════════════════════════════════════
        // ПРИВАТНЫЕ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>XYZ (double) → Coordinate (decimal). Только на границах входа/выхода.</summary>
        private static Coordinate XYZToCoordinate(XYZ p)
            => new Coordinate((decimal)p.X, (decimal)p.Y, (decimal)p.Z);

        private static ElementId GetLevelId(Element elem)
        {
            if (elem.LevelId != ElementId.InvalidElementId)
                return elem.LevelId;

            Parameter p = elem.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
            return p != null ? p.AsElementId() : ElementId.InvalidElementId;
        }

        private static ElementId GetSystemTypeId(Element elem)
        {
            return elem.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)
                       ?.AsElementId()
                   ?? ElementId.InvalidElementId;
        }
    }
}