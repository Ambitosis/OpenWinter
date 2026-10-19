// World presentation test: the caller can no longer change camera pitch or distance.
void main()
{
    LockCameraPitch(OBJECT_SELF, TRUE);
    LockCameraDistance(OBJECT_SELF, TRUE);
}
