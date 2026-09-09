using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Web.Migrations.AppDb
{
    /// <summary>
    ///     Заполнение разобранных колонок оружия и починка копий в листах персонажей.
    ///     <para>
    ///         Каталог: <c>RangeInfo</c>, <c>AttacksInfo</c>, <c>AmmoInfo</c>, <c>CostInfo</c>,
    ///         <c>MalfunctionThreshold</c> и <c>SkillId</c>. Значения JSONB сгенерированы
    ///         <c>WeaponStatsParser</c>&#8217;ом и привязаны к исходной строке, а не к <c>Id</c>:
    ///         одна запись на каждое различное значение колонки, а строка, которую Хранитель
    ///         успел поправить вручную, просто не совпадёт и останется пустой — такую запись
    ///         разберёт <c>WeaponStatsReader</c> на чтении.
    ///     </para>
    ///     <para>
    ///         Листы: копиям оружия внутри <c>Character-&gt;Weapons</c> проставляется
    ///         <c>CatalogWeaponId</c>, а собственный <c>Id</c> становится уникальным. Разобранные
    ///         блоки копиям не пишутся: их строки расходятся с каталогом (у «Colt M1903» в листе
    ///         дальность 10 м, в каталоге — 15), и подмена чужими числами противоречила бы тому,
    ///         что Хранитель видит на листе. Их разбирает <c>WeaponStatsReader</c>.
    ///     </para>
    ///     <para>Все шаги идемпотентны: повторный запуск ничего не меняет.</para>
    /// </summary>
    /// <inheritdoc />
    public partial class WeaponStatsBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Дальность: 28 различных значений колонки «Range»
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w SET "RangeInfo" = m.info::jsonb
                FROM (VALUES
                    ('10 метров', '{"Kind":2,"BaseMeters":10,"Bands":null,"ThrowDivisor":null,"RawText":"10 метров","IsParsed":true}'),
                    ('10/20/50 метров', '{"Kind":3,"BaseMeters":10,"Bands":[10,20,50],"ThrowDivisor":null,"RawText":"10/20/50 метров","IsParsed":true}'),
                    ('100 метров', '{"Kind":2,"BaseMeters":100,"Bands":null,"ThrowDivisor":null,"RawText":"100 метров","IsParsed":true}'),
                    ('110 метров', '{"Kind":2,"BaseMeters":110,"Bands":null,"ThrowDivisor":null,"RawText":"110 метров","IsParsed":true}'),
                    ('15 метров', '{"Kind":2,"BaseMeters":15,"Bands":null,"ThrowDivisor":null,"RawText":"15 метров","IsParsed":true}'),
                    ('150 метров', '{"Kind":2,"BaseMeters":150,"Bands":null,"ThrowDivisor":null,"RawText":"150 метров","IsParsed":true}'),
                    ('2 метра', '{"Kind":2,"BaseMeters":2,"Bands":null,"ThrowDivisor":null,"RawText":"2 метра","IsParsed":true}'),
                    ('20 метров', '{"Kind":2,"BaseMeters":20,"Bands":null,"ThrowDivisor":null,"RawText":"20 метров","IsParsed":true}'),
                    ('200 метров', '{"Kind":2,"BaseMeters":200,"Bands":null,"ThrowDivisor":null,"RawText":"200 метров","IsParsed":true}'),
                    ('2000 метров', '{"Kind":2,"BaseMeters":2000,"Bands":null,"ThrowDivisor":null,"RawText":"2000 метров","IsParsed":true}'),
                    ('25 метров', '{"Kind":2,"BaseMeters":25,"Bands":null,"ThrowDivisor":null,"RawText":"25 метров","IsParsed":true}'),
                    ('250 метров', '{"Kind":2,"BaseMeters":250,"Bands":null,"ThrowDivisor":null,"RawText":"250 метров","IsParsed":true}'),
                    ('3 метра', '{"Kind":2,"BaseMeters":3,"Bands":null,"ThrowDivisor":null,"RawText":"3 метра","IsParsed":true}'),
                    ('30 метров', '{"Kind":2,"BaseMeters":30,"Bands":null,"ThrowDivisor":null,"RawText":"30 метров","IsParsed":true}'),
                    ('3000 метров', '{"Kind":2,"BaseMeters":3000,"Bands":null,"ThrowDivisor":null,"RawText":"3000 метров","IsParsed":true}'),
                    ('5 метров', '{"Kind":2,"BaseMeters":5,"Bands":null,"ThrowDivisor":null,"RawText":"5 метров","IsParsed":true}'),
                    ('5/10 метров', '{"Kind":3,"BaseMeters":5,"Bands":[5,10],"ThrowDivisor":null,"RawText":"5/10 метров","IsParsed":true}'),
                    ('50 метров', '{"Kind":2,"BaseMeters":50,"Bands":null,"ThrowDivisor":null,"RawText":"50 метров","IsParsed":true}'),
                    ('500 метров', '{"Kind":2,"BaseMeters":500,"Bands":null,"ThrowDivisor":null,"RawText":"500 метров","IsParsed":true}'),
                    ('60 метров', '{"Kind":2,"BaseMeters":60,"Bands":null,"ThrowDivisor":null,"RawText":"60 метров","IsParsed":true}'),
                    ('80 метров', '{"Kind":2,"BaseMeters":80,"Bands":null,"ThrowDivisor":null,"RawText":"80 метров","IsParsed":true}'),
                    ('90 метров', '{"Kind":2,"BaseMeters":90,"Bands":null,"ThrowDivisor":null,"RawText":"90 метров","IsParsed":true}'),
                    ('Касание', '{"Kind":1,"BaseMeters":null,"Bands":null,"ThrowDivisor":null,"RawText":"Касание","IsParsed":true}'),
                    ('На месте', '{"Kind":5,"BaseMeters":null,"Bands":null,"ThrowDivisor":null,"RawText":"На месте","IsParsed":true}'),
                    ('Нет', '{"Kind":0,"BaseMeters":null,"Bands":null,"ThrowDivisor":null,"RawText":"Нет","IsParsed":true}'),
                    ('СИЛ / 5 метров', '{"Kind":4,"BaseMeters":null,"Bands":null,"ThrowDivisor":5,"RawText":"СИЛ / 5 метров","IsParsed":true}'),
                    ('СИЛ/5м', '{"Kind":4,"BaseMeters":null,"Bands":null,"ThrowDivisor":5,"RawText":"СИЛ/5м","IsParsed":true}'),
                    ('Сил/5м', '{"Kind":4,"BaseMeters":null,"Bands":null,"ThrowDivisor":5,"RawText":"Сил/5м","IsParsed":true}')
                ) AS m(raw, info)
                WHERE w."RangeInfo" IS NULL AND w."Range" = m.raw;
                """);

            // Число атак: 17 различных значений колонки «Attacks»
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w SET "AttacksInfo" = m.info::jsonb
                FROM (VALUES
                    ('1', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1","IsParsed":true}'),
                    ('1 (2)', '{"ShotsPerRound":1,"MaxShotsPerRound":2,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1 (2)","IsParsed":true}'),
                    ('1 (2) или непр. огонь', '{"ShotsPerRound":1,"MaxShotsPerRound":2,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":true,"IsSingleUse":false,"RawText":"1 (2) или непр. огонь","IsParsed":true}'),
                    ('1 (2) или очередями по 3', '{"ShotsPerRound":1,"MaxShotsPerRound":2,"RoundsPerAttack":null,"AllowsBurst":true,"BurstSize":3,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1 (2) или очередями по 3","IsParsed":true}'),
                    ('1 (3)', '{"ShotsPerRound":1,"MaxShotsPerRound":3,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1 (3)","IsParsed":true}'),
                    ('1 (3) или непр. огонь', '{"ShotsPerRound":1,"MaxShotsPerRound":3,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":true,"IsSingleUse":false,"RawText":"1 (3) или непр. огонь","IsParsed":true}'),
                    ('1 или 2', '{"ShotsPerRound":1,"MaxShotsPerRound":2,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1 или 2","IsParsed":true}'),
                    ('1 или непр. огонь', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":true,"IsSingleUse":false,"RawText":"1 или непр. огонь","IsParsed":true}'),
                    ('1 или очередями по 3', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":true,"BurstSize":3,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1 или очередями по 3","IsParsed":true}'),
                    ('1/2', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":2,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1/2","IsParsed":true}'),
                    ('1/3', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":3,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1/3","IsParsed":true}'),
                    ('1/4', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":4,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"1/4","IsParsed":true}'),
                    ('2', '{"ShotsPerRound":2,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"2","IsParsed":true}'),
                    ('На месте', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":true,"RawText":"На месте","IsParsed":true}'),
                    ('Непр. огонь', '{"ShotsPerRound":null,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":true,"IsSingleUse":false,"RawText":"Непр. огонь","IsParsed":true}'),
                    ('Нет', '{"ShotsPerRound":null,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":false,"RawText":"Нет","IsParsed":true}'),
                    ('Одноразовый', '{"ShotsPerRound":1,"MaxShotsPerRound":null,"RoundsPerAttack":null,"AllowsBurst":false,"BurstSize":null,"AllowsFullAuto":false,"IsSingleUse":true,"RawText":"Одноразовый","IsParsed":true}')
                ) AS m(raw, info)
                WHERE w."AttacksInfo" IS NULL AND w."Attacks" = m.raw;
                """);

            // Боезапас: 33 различных значений колонки «Ammo»
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w SET "AmmoInfo" = m.info::jsonb
                FROM (VALUES
                    ('', '{"Capacity":null,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"","IsParsed":true}'),
                    ('1', '{"Capacity":1,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"1","IsParsed":true}'),
                    ('10', '{"Capacity":10,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"10","IsParsed":true}'),
                    ('11', '{"Capacity":11,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"11","IsParsed":true}'),
                    ('15', '{"Capacity":15,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"15","IsParsed":true}'),
                    ('15/30', '{"Capacity":15,"CapacityOptions":[15,30],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"15/30","IsParsed":true}'),
                    ('17', '{"Capacity":17,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"17","IsParsed":true}'),
                    ('2', '{"Capacity":2,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"2","IsParsed":true}'),
                    ('20', '{"Capacity":20,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"20","IsParsed":true}'),
                    ('20/30/32', '{"Capacity":20,"CapacityOptions":[20,30,32],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"20/30/32","IsParsed":true}'),
                    ('20/30/50', '{"Capacity":20,"CapacityOptions":[20,30,50],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"20/30/50","IsParsed":true}'),
                    ('200', '{"Capacity":200,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"200","IsParsed":true}'),
                    ('25 доз', '{"Capacity":25,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"25 доз","IsParsed":true}'),
                    ('250', '{"Capacity":250,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"250","IsParsed":true}'),
                    ('3', '{"Capacity":3,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"3","IsParsed":true}'),
                    ('30', '{"Capacity":30,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"30","IsParsed":true}'),
                    ('30/100', '{"Capacity":30,"CapacityOptions":[30,100],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"30/100","IsParsed":true}'),
                    ('30/200', '{"Capacity":30,"CapacityOptions":[30,200],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"30/200","IsParsed":true}'),
                    ('32', '{"Capacity":32,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"32","IsParsed":true}'),
                    ('4000', '{"Capacity":4000,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"4000","IsParsed":true}'),
                    ('47/97', '{"Capacity":47,"CapacityOptions":[47,97],"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"47/97","IsParsed":true}'),
                    ('5', '{"Capacity":5,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"5","IsParsed":true}'),
                    ('6', '{"Capacity":6,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"6","IsParsed":true}'),
                    ('7', '{"Capacity":7,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"7","IsParsed":true}'),
                    ('8', '{"Capacity":8,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"8","IsParsed":true}'),
                    ('Автоподача', '{"Capacity":null,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":true,"IsSuppliedSeparately":false,"RawText":"Автоподача","IsParsed":true}'),
                    ('Варьирует', '{"Capacity":null,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Варьирует","IsParsed":false}'),
                    ('Минимум 10', '{"Capacity":10,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Минимум 10","IsParsed":true}'),
                    ('Однораз.', '{"Capacity":1,"CapacityOptions":null,"IsSingleUse":true,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Однораз.","IsParsed":true}'),
                    ('Одноразовая', '{"Capacity":1,"CapacityOptions":null,"IsSingleUse":true,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Одноразовая","IsParsed":true}'),
                    ('Одноразовый', '{"Capacity":1,"CapacityOptions":null,"IsSingleUse":true,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Одноразовый","IsParsed":true}'),
                    ('Отдельно', '{"Capacity":null,"CapacityOptions":null,"IsSingleUse":false,"IsBeltFed":false,"IsSuppliedSeparately":true,"RawText":"Отдельно","IsParsed":true}'),
                    ('Только 1', '{"Capacity":1,"CapacityOptions":null,"IsSingleUse":true,"IsBeltFed":false,"IsSuppliedSeparately":false,"RawText":"Только 1","IsParsed":true}')
                ) AS m(raw, info)
                WHERE w."AmmoInfo" IS NULL AND w."Ammo" = m.raw;
                """);

            // Стоимость: 75 различных значений колонки «Cost»
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w SET "CostInfo" = m.info::jsonb
                FROM (VALUES
                    ('$0,05/0,50', '{"Cost1920":0.05,"CostModern":0.50,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$0,05/0,50","IsParsed":true}'),
                    ('$0,50/3', '{"Cost1920":0.50,"CostModern":3,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$0,50/3","IsParsed":true}'),
                    ('$0,65 – 5,25', '{"Cost1920":0.65,"CostModern":null,"IsApproximate1920":true,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$0,65 – 5,25","IsParsed":true}'),
                    ('$1,98', '{"Cost1920":1.98,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$1,98","IsParsed":true}'),
                    ('$1/10', '{"Cost1920":1,"CostModern":10,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$1/10","IsParsed":true}'),
                    ('$1/20 коробка', '{"Cost1920":1,"CostModern":20,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$1/20 коробка","IsParsed":true}'),
                    ('$1/25', '{"Cost1920":1,"CostModern":25,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$1/25","IsParsed":true}'),
                    ('$10/100', '{"Cost1920":10,"CostModern":100,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$10/100","IsParsed":true}'),
                    ('$1000/20 000', '{"Cost1920":1000,"CostModern":20000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$1000/20 000","IsParsed":true}'),
                    ('$12/55', '{"Cost1920":12,"CostModern":55,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$12/55","IsParsed":true}'),
                    ('$13/70', '{"Cost1920":13,"CostModern":70,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$13/70","IsParsed":true}'),
                    ('$15/100', '{"Cost1920":15,"CostModern":100,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$15/100","IsParsed":true}'),
                    ('$15/200', '{"Cost1920":15,"CostModern":200,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$15/200","IsParsed":true}'),
                    ('$15/75', '{"Cost1920":15,"CostModern":75,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$15/75","IsParsed":true}'),
                    ('$1500/—', '{"Cost1920":1500,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":true,"RawText":"$1500/—","IsParsed":true}'),
                    ('$19/150', '{"Cost1920":19,"CostModern":150,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$19/150","IsParsed":true}'),
                    ('$2,50', '{"Cost1920":2.50,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2,50","IsParsed":true}'),
                    ('$2/15', '{"Cost1920":2,"CostModern":15,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2/15","IsParsed":true}'),
                    ('$2/4', '{"Cost1920":2,"CostModern":4,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2/4","IsParsed":true}'),
                    ('$2/5', '{"Cost1920":2,"CostModern":5,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2/5","IsParsed":true}'),
                    ('$2/6', '{"Cost1920":2,"CostModern":6,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2/6","IsParsed":true}'),
                    ('$20/200', '{"Cost1920":20,"CostModern":200,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$20/200","IsParsed":true}'),
                    ('$20/350', '{"Cost1920":20,"CostModern":350,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$20/350","IsParsed":true}'),
                    ('$200', '{"Cost1920":200,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$200","IsParsed":true}'),
                    ('$2000/14 000', '{"Cost1920":2000,"CostModern":14000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$2000/14 000","IsParsed":true}'),
                    ('$25/100', '{"Cost1920":25,"CostModern":100,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$25/100","IsParsed":true}'),
                    ('$25/150', '{"Cost1920":25,"CostModern":150,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$25/150","IsParsed":true}'),
                    ('$25/190', '{"Cost1920":25,"CostModern":190,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$25/190","IsParsed":true}'),
                    ('$25/200', '{"Cost1920":25,"CostModern":200,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$25/200","IsParsed":true}'),
                    ('$25/350', '{"Cost1920":25,"CostModern":350,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$25/350","IsParsed":true}'),
                    ('$3,75', '{"Cost1920":3.75,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3,75","IsParsed":true}'),
                    ('$3/35', '{"Cost1920":3,"CostModern":35,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3/35","IsParsed":true}'),
                    ('$3/9', '{"Cost1920":3,"CostModern":9,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3/9","IsParsed":true}'),
                    ('$30/300', '{"Cost1920":30,"CostModern":300,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$30/300","IsParsed":true}'),
                    ('$30/375', '{"Cost1920":30,"CostModern":375,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$30/375","IsParsed":true}'),
                    ('$30/75', '{"Cost1920":30,"CostModern":75,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$30/75","IsParsed":true}'),
                    ('$30/—', '{"Cost1920":30,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":true,"RawText":"$30/—","IsParsed":true}'),
                    ('$3000/20 000', '{"Cost1920":3000,"CostModern":20000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3000/20 000","IsParsed":true}'),
                    ('$3000/30 000', '{"Cost1920":3000,"CostModern":30000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3000/30 000","IsParsed":true}'),
                    ('$3000/50 000', '{"Cost1920":3000,"CostModern":50000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$3000/50 000","IsParsed":true}'),
                    ('$35/редкое', '{"Cost1920":35,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":true,"RawText":"$35/редкое","IsParsed":true}'),
                    ('$4/50', '{"Cost1920":4,"CostModern":50,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$4/50","IsParsed":true}'),
                    ('$40/200', '{"Cost1920":40,"CostModern":200,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$40/200","IsParsed":true}'),
                    ('$40/375', '{"Cost1920":40,"CostModern":375,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$40/375","IsParsed":true}'),
                    ('$40/редкое', '{"Cost1920":40,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":true,"RawText":"$40/редкое","IsParsed":true}'),
                    ('$400/1800', '{"Cost1920":400,"CostModern":1800,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$400/1800","IsParsed":true}'),
                    ('$45/100', '{"Cost1920":45,"CostModern":100,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$45/100","IsParsed":true}'),
                    ('$5/10', '{"Cost1920":5,"CostModern":10,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$5/10","IsParsed":true}'),
                    ('$5/50', '{"Cost1920":5,"CostModern":50,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$5/50","IsParsed":true}'),
                    ('$50/300', '{"Cost1920":50,"CostModern":300,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$50/300","IsParsed":true}'),
                    ('$7/75', '{"Cost1920":7,"CostModern":75,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$7/75","IsParsed":true}'),
                    ('$75/175', '{"Cost1920":75,"CostModern":175,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$75/175","IsParsed":true}'),
                    ('$75/600', '{"Cost1920":75,"CostModern":600,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$75/600","IsParsed":true}'),
                    ('$800/1500', '{"Cost1920":800,"CostModern":1500,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"$800/1500","IsParsed":true}'),
                    ('Нет', '{"Cost1920":null,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":true,"RawText":"Нет","IsParsed":true}'),
                    ('от $200 / $1600', '{"Cost1920":200,"CostModern":1600,"IsApproximate1920":true,"IsApproximateModern":false,"Unavailable1920":false,"UnavailableModern":false,"RawText":"от $200 / $1600","IsParsed":true}'),
                    ('—', '{"Cost1920":null,"CostModern":null,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":true,"RawText":"—","IsParsed":true}'),
                    ('—/$10', '{"Cost1920":null,"CostModern":10,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$10","IsParsed":true}'),
                    ('—/$1000', '{"Cost1920":null,"CostModern":1000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$1000","IsParsed":true}'),
                    ('—/$1100', '{"Cost1920":null,"CostModern":1100,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$1100","IsParsed":true}'),
                    ('—/$1500', '{"Cost1920":null,"CostModern":1500,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$1500","IsParsed":true}'),
                    ('—/$200', '{"Cost1920":null,"CostModern":200,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$200","IsParsed":true}'),
                    ('—/$2000', '{"Cost1920":null,"CostModern":2000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$2000","IsParsed":true}'),
                    ('—/$275', '{"Cost1920":null,"CostModern":275,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$275","IsParsed":true}'),
                    ('—/$2800', '{"Cost1920":null,"CostModern":2800,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$2800","IsParsed":true}'),
                    ('—/$300', '{"Cost1920":null,"CostModern":300,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$300","IsParsed":true}'),
                    ('—/$3000', '{"Cost1920":null,"CostModern":3000,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$3000","IsParsed":true}'),
                    ('—/$400', '{"Cost1920":null,"CostModern":400,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$400","IsParsed":true}'),
                    ('—/$425', '{"Cost1920":null,"CostModern":425,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$425","IsParsed":true}'),
                    ('—/$475', '{"Cost1920":null,"CostModern":475,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$475","IsParsed":true}'),
                    ('—/$500', '{"Cost1920":null,"CostModern":500,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$500","IsParsed":true}'),
                    ('—/$600', '{"Cost1920":null,"CostModern":600,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$600","IsParsed":true}'),
                    ('—/$650', '{"Cost1920":null,"CostModern":650,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$650","IsParsed":true}'),
                    ('—/$750', '{"Cost1920":null,"CostModern":750,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$750","IsParsed":true}'),
                    ('—/$895', '{"Cost1920":null,"CostModern":895,"IsApproximate1920":false,"IsApproximateModern":false,"Unavailable1920":true,"UnavailableModern":false,"RawText":"—/$895","IsParsed":true}')
                ) AS m(raw, info)
                WHERE w."CostInfo" IS NULL AND w."Cost" = m.raw;
                """);
            // Порог осечки числом. «00» на процентных костях — это 100; одиночный «0» —
            // незаполненное поле, а не оружие, заклинивающее при любом броске.
            migrationBuilder.Sql("""
                UPDATE games."Weapons"
                SET "MalfunctionThreshold" = CASE
                        WHEN regexp_replace("Malfunction", '[^0-9]', '', 'g') ~ '^00+$' THEN 100
                        ELSE regexp_replace("Malfunction", '[^0-9]', '', 'g')::int
                    END
                WHERE "MalfunctionThreshold" IS NULL
                  AND regexp_replace("Malfunction", '[^0-9]', '', 'g') ~ '^[0-9]+$'
                  AND (regexp_replace("Malfunction", '[^0-9]', '', 'g') ~ '^00+$'
                       OR regexp_replace("Malfunction", '[^0-9]', '', 'g')::numeric BETWEEN 1 AND 100);
                """);

            // Навык из справочника. После «Каталога по таблице XVII» все 108 записей несут
            // каноническое имя навыка, поэтому хватает точного сравнения.
            migrationBuilder.Sql("""
                UPDATE games."Weapons" w SET "SkillId" = s."Id"
                FROM games."Skills" s
                WHERE w."SkillId" IS NULL AND w."Skill" = s."Name";
                """);

            // Копии оружия в JSONB листов персонажей.
            //
            // Шаг 1 — копии, чей Id совпадает с каталожной записью: в лист попал сам
            //          справочный экземпляр. Id переезжает в CatalogWeaponId, а копия
            //          получает собственный Guid v7.
            // Шаг 2 — копии, узнаваемые по имени («Револьвер .38 кал.» и прочие
            //          сокращения): проставляется CatalogWeaponId.
            // Шаг 3 — остальное («Драка», «Ритуальный кинжал») — законно самодельное:
            //          CatalogWeaponId остаётся пустым, но нулевые Id заменяются на Guid v7.
            //
            // Строка обновляется, только если хоть одному её оружию есть что менять, —
            // поэтому повторный запуск ничего не трогает.
            migrationBuilder.Sql("""
                WITH name_map(sheet_name, catalog_name) AS (VALUES
                    ('Colt M1903 (.32 калибра)',    'Автоматический пистолет 32-го калибра (7,65 мм)'),
                    ('Малый дерринджер .22',        '«Дерринджер» 25-го калибра (IC)'),
                    ('Полицейская дубинка',         'Дубина, малая (полицейская дубинка)'),
                    ('Револьвер .38 кал.',          'Револьвер 38-го калибра (9 мм)'),
                    ('Револьвер .45 кал.',          'Револьвер 45-го калибра'),
                    ('Револьвер S&W 38-го калибра', 'Револьвер 38-го калибра (9 мм)')
                ),
                elements AS (
                    SELECT ch."Id" AS character_id, src.ord, src.w
                    FROM games."Characters" ch,
                         jsonb_array_elements(ch."Character"->'Weapons') WITH ORDINALITY AS src(w, ord)
                    WHERE jsonb_typeof(ch."Character"->'Weapons') = 'array'
                ),
                resolved AS (
                    SELECT e.character_id, e.ord, e.w,
                           by_id."Id" IS NOT NULL AS id_taken_from_catalog,
                           COALESCE(by_id."Id", by_name."Id") AS catalog_id,
                           (e.w->>'Id' IS NULL
                            OR e.w->>'Id' = '00000000-0000-0000-0000-000000000000') AS id_is_empty,
                           regexp_replace(COALESCE(e.w->>'Malfunction', ''), '[^0-9]', '', 'g') AS malf
                    FROM elements e
                    LEFT JOIN games."Weapons" by_id ON by_id."Id"::text = e.w->>'Id'
                    LEFT JOIN name_map nm ON nm.sheet_name = e.w->>'Name'
                    LEFT JOIN games."Weapons" by_name ON by_name."Name" = nm.catalog_name
                ),
                prepared AS (
                    SELECT r.*,
                           CASE
                               WHEN r.malf ~ '^00+$' THEN 100
                               WHEN r.malf ~ '^[0-9]+$' AND r.malf::numeric BETWEEN 1 AND 100
                                   THEN r.malf::int
                           END AS threshold
                    FROM resolved r
                ),
                marked AS (
                    SELECT p.*,
                           (p.id_taken_from_catalog OR p.id_is_empty
                            OR (p.catalog_id IS NOT NULL AND p.w->>'CatalogWeaponId' IS NULL)
                            OR (p.threshold IS NOT NULL
                                AND p.w->>'MalfunctionThreshold' IS NULL)) AS needs_fix
                    FROM prepared p
                ),
                rebuilt AS (
                    SELECT m.character_id,
                           bool_or(m.needs_fix) AS any_fix,
                           jsonb_agg(
                               m.w
                               || CASE
                                      WHEN m.id_taken_from_catalog OR m.id_is_empty THEN
                                          jsonb_build_object('Id', (
                                              lpad(to_hex((extract(epoch FROM clock_timestamp()) * 1000)::bigint), 12, '0')
                                              || '7' || substring(md5(random()::text || clock_timestamp()::text) FROM 1 FOR 3)
                                              || to_hex(8 + (random() * 3)::int)
                                              || substring(md5(random()::text || clock_timestamp()::text) FROM 4 FOR 15)
                                          )::uuid)
                                      ELSE '{}'::jsonb
                                  END
                               || CASE
                                      WHEN m.catalog_id IS NOT NULL AND m.w->>'CatalogWeaponId' IS NULL
                                          THEN jsonb_build_object('CatalogWeaponId', m.catalog_id)
                                      ELSE '{}'::jsonb
                                  END
                               || CASE
                                      WHEN m.threshold IS NOT NULL AND m.w->>'MalfunctionThreshold' IS NULL
                                          THEN jsonb_build_object('MalfunctionThreshold', m.threshold)
                                      ELSE '{}'::jsonb
                                  END
                               ORDER BY m.ord) AS weapons
                    FROM marked m
                    GROUP BY m.character_id
                )
                UPDATE games."Characters" ch
                SET "Character" = jsonb_set(ch."Character", '{Weapons}', rebuilt.weapons),
                    "LastUpdated" = now()
                FROM rebuilt
                WHERE rebuilt.character_id = ch."Id" AND rebuilt.any_fix;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратного хода нет: колонки убирает WeaponTypedStats, а восстанавливать
            // старые (неуникальные и нулевые) Id копий в листах нечем и незачем.
        }
    }
}
