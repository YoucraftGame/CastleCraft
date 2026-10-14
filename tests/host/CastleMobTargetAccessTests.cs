using System;
using PeakCraft.World;

internal static class CastleMobTargetAccessTests
{
    internal static void Run(Action<string, Action> check, Action<bool> must)
    {
        check("Combat target health keeps native property clamping and update order", () => {
            var target = new Inspector();
            CastleMobTargetAccess.Set(target, "maxHp", 100);
            CastleMobTargetAccess.Set(target, "CurrentHp", 150);
            must((int)CastleMobTargetAccess.Get(target, "CurrentHp")! == 100);
            CastleMobTargetAccess.Set(target, "CurrentHp", -5);
            must((int)CastleMobTargetAccess.Get(target, "CurrentHp")! == 0 && target.Writes == 2);
        });
        check("Cached combat metadata never shares values between entities", () => {
            var a = new Inspector(); var b = new Inspector();
            CastleMobTargetAccess.Set(a, "maxHp", 200); CastleMobTargetAccess.Set(b, "maxHp", 100);
            for (int i = 0; i < 1000; i++) {
                CastleMobTargetAccess.Set(a, "CurrentHp", i % 200);
                CastleMobTargetAccess.Set(b, "CurrentHp", i % 100);
                must((int)CastleMobTargetAccess.Get(a, "CurrentHp")! == i % 200
                    && (int)CastleMobTargetAccess.Get(b, "CurrentHp")! == i % 100);
            }
            must(a.Writes == 1000 && b.Writes == 1000);
        });
        check("Inherited private combat fields and properties remain accessible", () => {
            var target = new Derived();
            CastleMobTargetAccess.Set(target, "body", 42);
            CastleMobTargetAccess.Set(target, "HiddenHealth", 35);
            must((int)CastleMobTargetAccess.Get(target, "body")! == 42
                && (int)CastleMobTargetAccess.Get(target, "HiddenHealth")! == 35);
        });
        check("Member cache distinguishes runtime types with the same member name", () => {
            var field = new FieldHealth(); var property = new Inspector();
            CastleMobTargetAccess.Set(property, "maxHp", 100);
            CastleMobTargetAccess.Set(field, "CurrentHp", 150);
            CastleMobTargetAccess.Set(property, "CurrentHp", 150);
            must((int)CastleMobTargetAccess.Get(field, "CurrentHp")! == 150
                && (int)CastleMobTargetAccess.Get(property, "CurrentHp")! == 100);
        });
        check("Optional missing combat members remain null and no-op", () => {
            var target = new Inspector();
            for (int i = 0; i < 1000; i++) {
                CastleMobTargetAccess.Set(target, "Missing", i);
                must(CastleMobTargetAccess.Get(target, "Missing") == null);
            }
            must(target.Writes == 0);
        });
        check("Null field still falls back to inherited property and writes prefer field", () => {
            var target = new Shadow();
            must((string)CastleMobTargetAccess.Get(target, "Value")! == "fallback");
            CastleMobTargetAccess.Set(target, "Value", "field");
            must((string)CastleMobTargetAccess.Get(target, "Value")! == "field");
        });
    }
    private sealed class Inspector
    {
        public int maxHp { get; set; }
        private int hp;
        public int Writes;
        public int CurrentHp { get => hp; set { hp = Math.Clamp(value, 0, maxHp); Writes++; } }
    }
    private class Base
    {
        private int body = 0;
        private int HiddenHealth { get; set; }
        public int ReadBody() => body;
    }
    private sealed class Derived : Base { }
    private sealed class FieldHealth { public int CurrentHp = 0; }
    private class Fallback { public string Value => "fallback"; }
    private sealed class Shadow : Fallback { public new string? Value = null; }
}
