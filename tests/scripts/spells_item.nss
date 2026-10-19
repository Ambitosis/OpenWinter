// Run on the caster: use the potion's cast-spell property on itself.
void main()
{
    object oPotion = GetObjectByTag("spell_potion");
    ActionUseItemOnObject(oPotion, GetFirstItemProperty(oPotion), OBJECT_SELF);
}
