// World presentation test: the test cow wears the ox's skin, and every player sees tree bark with the cow's hide.
#include "fx_target"
void main()
{
    ReplaceObjectTexture(FxTarget(), "c_a_cow", "c_a_ox");
    SetTextureOverride("TCN01_bark02", "c_a_cow");
}
