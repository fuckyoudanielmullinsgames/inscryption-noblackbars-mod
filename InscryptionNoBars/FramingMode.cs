namespace InscryptionNoBars
{
    /// <summary>
    /// What to do with the space that the vertical black bars used to occupy.
    /// </summary>
    internal enum FramingMode
    {
        /// <summary>
        /// Keep the game's horizontal framing untouched and reveal more world vertically.
        /// Nothing gets cropped, nothing is distorted, and the card battle composition (which is
        /// designed around a fixed horizontal layout) stays exactly as intended.
        /// </summary>
        PreserveHorizontal = 0,

        /// <summary>
        /// Keep the vertical framing untouched and reveal more world horizontally. This is the
        /// classic ultrawide behaviour, but 2D/GBC scenes are authored to fill the frame, so the
        /// extra space at the sides can end up empty.
        /// </summary>
        PreserveVertical = 1
    }
}