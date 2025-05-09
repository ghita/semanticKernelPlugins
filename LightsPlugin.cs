using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace FirstPlugin;

public class LightsPlugin
{
   // Mock data for the lights
   private readonly List<LightModel> lights = new()
   {
      new LightModel { Id = 1, Name = "Table Lamp", IsOn = false, Brightness = 100, Hex = "FF0000" },
      new LightModel { Id = 2, Name = "Porch light", IsOn = false, Brightness = 50, Hex = "00FF00" },
      new LightModel { Id = 3, Name = "Chandelier", IsOn = true, Brightness = 75, Hex = "0000FF" }
   };

   [KernelFunction("get_lights")]
   [Description("Gets a list of lights and their current state")]
   public async Task<List<LightModel>> GetLightsAsync()
   {
      return await Task.FromResult(lights);
   }

   [KernelFunction("get_state")]
   [Description("Gets the state of a particular light")]
   public async Task<LightModel?> GetStateAsync([Description("The ID of the light")] int id)
   {
      // Get the state of the light with the specified ID
      return await Task.FromResult(lights.FirstOrDefault(light => light.Id == id));
   }

   [KernelFunction("change_state")]
   [Description("Changes the state of the light")]
   public async Task<LightModel?> ChangeStateAsync(int id, LightModel LightModel)
   {
      var light = lights.FirstOrDefault(light => light.Id == id);

      if (light == null)
      {
         return null;
      }

      // Update the light with the new state
      light.IsOn = LightModel.IsOn;
      light.Brightness = LightModel.Brightness;
      light.Hex = LightModel.Hex;

      return await Task.FromResult(light);
   }
}

public class LightModel
{
   [JsonPropertyName("id")]
   required public int Id { get; set; }

   [JsonPropertyName("name")]
   required public string Name { get; set; }

   [JsonPropertyName("is_on")]
   public bool? IsOn { get; set; }

   [JsonPropertyName("brightness")]
   public byte? Brightness { get; set; }

   [JsonPropertyName("hex")]
   public string? Hex { get; set; }
}