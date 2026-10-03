using System.Text.Json.Serialization;

namespace IngaCookBook.SharedKernel.Notebook;

/// <summary>Transport alternatives for a notebook edit; concrete cases indicate success or rejection.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ChangeSaved), "saved")]
[JsonDerivedType(typeof(ChangeRejected), "rejected")]
public abstract record NotebookChange;
