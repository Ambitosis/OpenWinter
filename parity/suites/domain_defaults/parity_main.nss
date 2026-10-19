#include "parity_inc"
void Report(string key,object c,int cls) {
 PLogI(key+".catsReady",GetMemorizedSpellReady(c,cls,2,0));
 PLogI(key+".catsEffect",GetHasSpellEffect(13,c));
 PLogI(key+".mageReady",GetMemorizedSpellReady(c,cls,1,0));
 PLogI(key+".mageEffect",GetHasSpellEffect(102,c));
}
void Case(string name,int cls,float x) {
 object c=CreateObject(OBJECT_TYPE_CREATURE,name,PCentre(x,0.0));SetAILevel(c,AI_LEVEL_NORMAL);
 PLogI(name+".classLevel",GetLevelByClass(cls,c));
 PLogI(name+".domain1",GetDomain(c,1,cls));PLogI(name+".domain2",GetDomain(c,2,cls));
 PLogI(name+".cleric1",GetDomain(c,1,CLASS_TYPE_CLERIC));
 PLogI(name+".badindex0",GetDomain(c,0,cls));PLogI(name+".badindex3",GetDomain(c,3,cls));
 if(cls==CLASS_TYPE_FIGHTER)return;
 SetMemorizedSpell(c,cls,1,0,102,TRUE,0,1);
 SetMemorizedSpell(c,cls,2,0,13,TRUE,0,1);
 DelayCommand(2.0,AssignCommand(c,ActionCastSpellAtObject(13,c,0,FALSE,2,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls)));
 DelayCommand(8.0,Report(name+".cats",c,cls));
 DelayCommand(9.0,AssignCommand(c,ActionCastSpellAtObject(102,c,0,FALSE,1,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls)));
 DelayCommand(15.0,Report(name+".mage",c,cls));
 if(cls==CLASS_TYPE_WIZARD) {
  DelayCommand(16.0,AssignCommand(c,ActionCastSpellAtObject(13,c,0,FALSE,0,PROJECTILE_PATH_TYPE_DEFAULT,TRUE,cls)));
  DelayCommand(22.0,Report(name+".ordinary",c,cls));
 }
}
void main(){
 Case("dd_none",2,0.0);
 Case("dd_first",2,3.0);
 Case("dd_second",2,6.0);
 Case("dd_invalid1",2,9.0);
 Case("dd_invalid2",2,12.0);
 Case("dd_invalid",2,15.0);
 Case("dd_duplicate",2,18.0);
 Case("dd_airtwice",2,21.0);
 Case("dd_wizard",10,24.0);
 Case("dd_wizfields",10,27.0);
 Case("dd_fighter",4,30.0);
 Case("dd_fightfields",4,33.0);
 DelayCommand(23.0,PDone());
}
