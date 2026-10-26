// The merchant barrel opens the store for whoever uses it.
void main()
{
    OpenStore(GetObjectByTag("BR_STORE"), GetLastUsedBy());
}
