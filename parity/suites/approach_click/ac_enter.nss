// Trigger OnEnter: who came in.
void main(){object o=GetEnteringObject();SetLocalInt(o,"ENTERED_"+GetTag(OBJECT_SELF),1);}
