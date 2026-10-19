void main()
{
    string key=GetLocalString(OBJECT_SELF,"case")+".exits";
    SetLocalInt(GetModule(),key,GetLocalInt(GetModule(),key)+1);
}
