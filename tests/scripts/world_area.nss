// Area instancing: CreateArea, CopyArea, DestroyArea, GetFirst/NextArea.
int CountAreas()
{
    int n = 0;
    object a = GetFirstArea();
    while (GetIsObjectValid(a)) { n++; a = GetNextArea(); }
    return n;
}

void main()
{
    object m = GetModule();
    SetLocalInt(m, "before", CountAreas());
    object oNew = CreateArea("ss_town", "WORLD_TOWN2", "Second Town");
    SetLocalObject(m, "created", oNew);
    SetLocalString(m, "created_resref", GetResRef(oNew));
    SetLocalString(m, "created_tag", GetTag(oNew));
    SetLocalString(m, "created_name", GetName(oNew));
    SetLocalInt(m, "after", CountAreas());
    object oCopy = CopyArea(oNew, "WORLD_TOWN3");
    SetLocalObject(m, "copy", oCopy);
    SetLocalString(m, "copy_resref", GetResRef(oCopy));
    SetLocalInt(m, "copy_width", GetAreaSize(AREA_WIDTH, oCopy));
    SetLocalInt(m, "bad_create", GetIsObjectValid(CreateArea("no_such_area")));
    SetLocalInt(m, "destroy_invalid", DestroyArea(OBJECT_INVALID));
    SetLocalInt(m, "destroy_copy", DestroyArea(oCopy));
    SetLocalInt(m, "copy_valid", GetIsObjectValid(oCopy));
    SetLocalInt(m, "final", CountAreas());
}
