// World presentation test: undoes fx_texture.
#include "fx_target"
void main()
{
    ReplaceObjectTexture(FxTarget(), "c_a_cow", "");
    SetTextureOverride("TCN01_bark02", "");
}
