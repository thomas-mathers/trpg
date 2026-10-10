namespace TRPG.Domain.Models;

// Pure decor with no gameplay behavior; its look comes from the persisted Model.
public class Furniture : Prop
{
    public required PropModel Model { get; init; }

    public override bool BlocksMovement =>
        Model
            is not (
                PropModel.FurnitureRug
                or PropModel.FurnitureChandelier
                or PropModel.FurnitureWallSconce
                or PropModel.FurnitureWallLantern
                or PropModel.FurnitureBanner
            );
}
