// World presentation test: the test cow's idle animation is replaced by its death pose.
#include "fx_target"
void main()
{
    ReplaceObjectAnimation(FxTarget(), "cpause1", "cdead");
}
