# P2P selector discriminator

Eight wholly authored models compare ASCII `p2p_sel` 0, 1 and 2 with identical
parameters. Cases 0–2 use Normal rendering; cases 3–5 use Linked. All have P2P
enabled, velocity 4, life 1.5, gravity 20, zero Bezier handles, explicit 1x1 grids,
and the same direct reattachable reference child.

The earlier `beamp2pgravity` scene is retained unchanged, but its name and original
capture metadata incorrectly presumed selector 0 meant gravity. Its results are
only selector-0 evidence. The `p2p_sel` label seen in binary models is not an
ASCII enum definition. No runtime correction is inferred from it.

This new control is prepared, not yet measured. It must pass all sixteen actor/model
gates, all 41 framebuffer captures and normal completion before interpretation.
Use the guarded native harness; preserve raw framebuffers and Windows negatives.

Cases 6/7 retain Normal rendering and add named `p2p_type Gravity` with selector 1,
and `p2p_type Bezier` with selector 0 respectively. This explicitly tests named
field precedence: clean stock `c_mindalhoon` uses Gravity with numeric 2, while
the public format description lists Gravity as numeric 0. Neither mapping nor
precedence is inferred solely from this disagreement.
