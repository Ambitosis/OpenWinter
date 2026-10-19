#include "parity_itemev"
// Creature OnSpawn: the creature and how many items it holds now.
void main()
{
    PEvAdd("spawn:" + PEvName(OBJECT_SELF) + "[" + PEvItems(OBJECT_SELF) + "]");
}
