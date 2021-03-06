﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2012-2016.

// NOTE: this file is about twice as long as should be permissible, but the fact that I have to define mutally
// recursive types in the same file, as is also the case for abstract data types and their functions, prevents me from
// separating things out. This is doesn't really have a big impact on modularity - mostly it just hampers code
// organization.

namespace Debug
open System
module internal World =

    /// The value of the world currently chosen for debugging in an IDE. Not to be used for anything else.
    let mutable internal Chosen = obj ()
    let mutable internal viewGame = fun (_ : obj) -> Array.create 0 (String.Empty, obj ())
    let mutable internal viewScreen = fun (_ : obj) (_ : obj) -> Array.create 0 (String.Empty, obj ())
    let mutable internal viewGroup = fun (_ : obj) (_ : obj) -> Array.create 0 (String.Empty, obj ())
    let mutable internal viewEntity = fun (_ : obj) (_ : obj) -> Array.create 0 (String.Empty, obj ())

namespace Nu
open System
open System.Diagnostics
open FSharpx.Collections
open OpenTK
open TiledSharp
open Prime
open Nu

/// The type of a screen transition. Incoming means a new screen is being shown, and Outgoing
/// means an existing screen being hidden.
type TransitionType =
    | Incoming
    | Outgoing

/// The state of a screen's transition.
type TransitionState =
    | IncomingState
    | OutgoingState
    | IdlingState

/// Describes one of a screen's transition processes.
type [<CLIMutable; StructuralEquality; NoComparison>] Transition =
    { TransitionType : TransitionType
      TransitionLifetime : int64
      OptDissolveImage : AssetTag option }

    /// Make a screen transition.
    static member make transitionType =
        { TransitionType = transitionType
          TransitionLifetime = 0L
          OptDissolveImage = None }

/// Describes the behavior of the screen dissolving algorithm.
type [<StructuralEquality; NoComparison>] DissolveData =
    { IncomingTime : int64
      OutgoingTime : int64
      DissolveImage : AssetTag }

/// Describes the behavior of the screen splash algorithm.
type [<StructuralEquality; NoComparison>] SplashData =
    { DissolveData : DissolveData
      IdlingTime : int64
      SplashImage : AssetTag }

/// The data needed to describe a Tiled tile map.
type [<StructuralEquality; NoComparison>] TileMapData =
    { Map : TmxMap
      MapSize : Vector2i
      TileSize : Vector2i
      TileSizeF : Vector2
      TileMapSize : Vector2i
      TileMapSizeF : Vector2
      TileSet : TmxTileset
      TileSetSize : Vector2i }

/// The data needed to describe a Tiled tile.
type [<StructuralEquality; NoComparison>] TileData =
    { Tile : TmxLayerTile
      I : int
      J : int
      Gid : int
      GidPosition : int
      Gid2 : Vector2i
      OptTileSetTile : TmxTilesetTile option
      TilePosition : Vector2i }

[<AutoOpen>]
module WorldTypes =

    /// A simulant in the world.
    type Simulant =
        interface
            inherit Participant
            abstract member SimulantAddress : Simulant Address
            end

    /// Operators for the Simulant type.
    type SimulantOperators =
        private
            | SimulantOperators

        /// Concatenate two addresses, forcing the type of first address.
        static member acatf<'a> (address : 'a Address) (simulant : Simulant) = acatf address (atooa simulant.SimulantAddress)

        /// Concatenate two addresses, takings the type of first address.
        static member (->-) (address, simulant : Simulant) = SimulantOperators.acatf address simulant

    /// The data for a change in the world's ambient state.
    type [<StructuralEquality; NoComparison>] AmbientChangeData = 
        { OldWorldWithOldState : World }
    
    /// The default dispatcher for games.
    and GameDispatcher () =
    
        /// Register a game when adding it to the world. Note that there is no corresponding
        /// Unregister method due to the inability to remove a game from the world.
        abstract Register : Game * World -> World
        default dispatcher.Register (_, world) = world
    
        /// Update a game.
        abstract Update : Game * World -> World
        default dispatcher.Update (_, world) = world
    
        /// Actualize a game.
        abstract Actualize : Game * World -> World
        default dispatcher.Actualize (_, world) = world
    
    /// The default dispatcher for screens.
    and ScreenDispatcher () =
    
        static member PropertyDefinitions =
            [Define? OptSpecialization (None : string option)
             Define? Persistent true]
    
        /// Register a screen when adding it to the world.
        abstract Register : Screen * World -> World
        default dispatcher.Register (_, world) = world
    
        /// Unregister a screen when removing it from the world.
        abstract Unregister : Screen * World -> World
        default dispatcher.Unregister (_, world) = world
    
        /// Update a screen.
        abstract Update : Screen * World -> World
        default dispatcher.Update (_, world) = world
    
        /// Actualize a screen.
        abstract Actualize : Screen * World -> World
        default dispatcher.Actualize (_, world) = world
    
    /// The default dispatcher for groups.
    and GroupDispatcher () =
    
        static member PropertyDefinitions =
            [Define? OptSpecialization (None : string option)
             Define? Persistent true]
    
        /// Register a group when adding it to a screen.
        abstract Register : Group * World -> World
        default dispatcher.Register (_, world) = world
    
        /// Unregister a group when removing it from a screen.
        abstract Unregister : Group * World -> World
        default dispatcher.Unregister (_, world) = world
    
        /// Update a group.
        abstract Update : Group * World -> World
        default dispatcher.Update (_, world) = world
    
        /// Actualize a group.
        abstract Actualize : Group * World -> World
        default dispatcher.Actualize (_, world) = world
    
    /// The default dispatcher for entities.
    and EntityDispatcher () =
    
        static member PropertyDefinitions =
            [Define? OptSpecialization (None : string option)
             Define? Position Vector2.Zero
             Define? Size Constants.Engine.DefaultEntitySize
             Define? Rotation 0.0f
             Define? Depth 0.0f
             Define? Overflow Vector2.Zero
             Define? ViewType Relative
             Define? Visible true
             Define? Omnipresent false
             Define? PublishUpdatesNp false
             Define? PublishChangesNp false
             Define? Persistent true]
    
        /// Register an entity when adding it to a group.
        abstract Register : Entity * World -> World
        default dispatcher.Register (_, world) = world
    
        /// Unregister an entity when removing it from a group.
        abstract Unregister : Entity * World -> World
        default dispatcher.Unregister (_, world) = world
    
        /// Propagate an entity's physics properties from the physics subsystem.
        abstract PropagatePhysics : Entity * World -> World
        default dispatcher.PropagatePhysics (_, world) = world
    
        /// Update an entity.
        abstract Update : Entity * World -> World
        default dispatcher.Update (_, world) = world
    
        /// Actualize an entity.
        abstract Actualize : Entity * World -> World
        default dispatcher.Actualize (_, world) = world
    
        /// Get the quick size of an entity (the appropriate user-define size for an entity).
        abstract GetQuickSize : Entity * World -> Vector2
        default dispatcher.GetQuickSize (_, _) = Vector2.One
    
        /// Get the priority with which an entity is picked in the editor.
        abstract GetPickingPriority : Entity * single * World -> single
        default dispatcher.GetPickingPriority (_, depth, _) = depth
    
    /// Dynamically augments an entity's behavior in a composable way.
    and Facet () =
    
        /// Register a facet when adding it to an entity.
        abstract Register : Entity * World -> World
        default facet.Register (entity, world) = facet.RegisterPhysics (entity, world)
    
        /// Unregister a facet when removing it from an entity.
        abstract Unregister : Entity * World -> World
        default facet.Unregister (entity, world) = facet.UnregisterPhysics (entity, world)
    
        /// Participate in the registration of an entity's physics with the physics subsystem.
        abstract RegisterPhysics : Entity * World -> World
        default facet.RegisterPhysics (_, world) = world
    
        /// Participate in the unregistration of an entity's physics from the physics subsystem.
        abstract UnregisterPhysics : Entity * World -> World
        default facet.UnregisterPhysics (_, world) = world
    
        /// Participate in the propagation an entity's physics properties from the physics subsystem.
        abstract PropagatePhysics : Entity * World -> World
        default facet.PropagatePhysics (_, world) = world
    
        /// Update a facet.
        abstract Update : Entity * World -> World
        default facet.Update (_, world) = world
    
        /// Actualize a facet.
        abstract Actualize : Entity * World -> World
        default facet.Actualize (_, world) = world
    
        /// Participate in getting the priority with which an entity is picked in the editor.
        abstract GetQuickSize : Entity * World -> Vector2
        default facet.GetQuickSize (_, _) = Constants.Engine.DefaultEntitySize
    
    /// Hosts the ongoing state of a game. The end-user of this engine should never touch this
    /// type, and it's public _only_ to make [<CLIMutable>] work.
    and [<CLIMutable; NoEquality; NoComparison>] GameState =
        { Id : Guid
          Xtension : Xtension
          DispatcherNp : GameDispatcher
          CreationTimeStampNp : int64
          OptSelectedScreen : Screen option
          OptScreenTransitionDestination : Screen option }

        /// The dynamic look-up operator.
        static member get gameState propertyName : 'a =
            Xtension.(?) (gameState.Xtension, propertyName)

        /// The dynamic assignment operator.
        static member set gameState propertyName (value : 'a) =
            { gameState with GameState.Xtension = Xtension.(?<-) (gameState.Xtension, propertyName, value) }

        /// Attach a dynamic property.
        static member attachProperty name value gameState =
            { gameState with GameState.Xtension = Xtension.attachProperty name { PropertyValue = value; PropertyType = getType value } gameState.Xtension }
    
        /// Make a game state value.
        static member make dispatcher =
            { Id = makeGuid ()
              Xtension = Xtension.safe
              DispatcherNp = dispatcher
              CreationTimeStampNp = Core.getTimeStamp ()
              OptSelectedScreen = None
              OptScreenTransitionDestination = None }

        /// Copy a game such as when, say, you need it to be mutated with reflection but you need to preserve persistence.
        static member copy this =
            { this with GameState.Id = this.Id }
    
    /// Hosts the ongoing state of a screen. The end-user of this engine should never touch this
    /// type, and it's public _only_ to make [<CLIMutable>] work.
    and [<CLIMutable; NoEquality; NoComparison>] ScreenState =
        { Id : Guid
          Name : Name
          Xtension : Xtension
          DispatcherNp : ScreenDispatcher
          CreationTimeStampNp : int64
          EntityTreeNp : Entity QuadTree MutantCache
          OptSpecialization : string option
          TransitionStateNp : TransitionState
          TransitionTicksNp : int64
          Incoming : Transition
          Outgoing : Transition
          Persistent : bool }

        /// The dynamic look-up operator.
        static member get screenState propertyName : 'a =
            Xtension.(?) (screenState.Xtension, propertyName)

        /// The dynamic assignment operator.
        static member set screenState propertyName (value : 'a) =
            { screenState with ScreenState.Xtension = Xtension.(?<-) (screenState.Xtension, propertyName, value) }

        /// Attach a dynamic property.
        static member attachProperty name value screenState =
            { screenState with ScreenState.Xtension = Xtension.attachProperty name { PropertyValue = value; PropertyType = getType value } screenState.Xtension }
    
        /// Make a screen state value.
        static member make optSpecialization optName dispatcher =
            let (id, name) = Reflection.deriveNameAndId optName
            let screenState =
                { Id = id
                  Name = name
                  Xtension = Xtension.safe
                  DispatcherNp = dispatcher
                  CreationTimeStampNp = Core.getTimeStamp ()
                  EntityTreeNp = Unchecked.defaultof<Entity QuadTree MutantCache>
                  OptSpecialization = optSpecialization 
                  TransitionStateNp = IdlingState
                  TransitionTicksNp = 0L // TODO: roll this field into Incoming/OutcomingState values
                  Incoming = Transition.make Incoming
                  Outgoing = Transition.make Outgoing
                  Persistent = true }
            let quadTree = QuadTree.make Constants.Engine.EntityTreeDepth Constants.Engine.EntityTreeBounds
            { screenState with EntityTreeNp = MutantCache.make Operators.id quadTree }

        /// Copy a screen such as when, say, you need it to be mutated with reflection but you need to preserve persistence.
        static member copy this =
            { this with ScreenState.Id = this.Id }
    
    /// Hosts the ongoing state of a group. The end-user of this engine should never touch this
    /// type, and it's public _only_ to make [<CLIMutable>] work.
    and [<CLIMutable; NoEquality; NoComparison>] GroupState =
        { Id : Guid
          Name : Name
          Xtension : Xtension
          DispatcherNp : GroupDispatcher
          CreationTimeStampNp : int64
          OptSpecialization : string option
          Persistent : bool }

        /// The dynamic look-up operator.
        static member get groupState propertyName : 'a =
            Xtension.(?) (groupState.Xtension, propertyName)

        /// The dynamic assignment operator.
        static member set groupState propertyName (value : 'a) =
            { groupState with GroupState.Xtension = Xtension.(?<-) (groupState.Xtension, propertyName, value) }

        /// Attach a dynamic property.
        static member attachProperty name value groupState =
            { groupState with GroupState.Xtension = Xtension.attachProperty name { PropertyValue = value; PropertyType = getType value } groupState.Xtension }
    
        /// Make a group state value.
        static member make optSpecialization optName dispatcher =
            let (id, name) = Reflection.deriveNameAndId optName
            { GroupState.Id = id
              Name = name
              Xtension = Xtension.safe
              DispatcherNp = dispatcher
              CreationTimeStampNp = Core.getTimeStamp ()
              OptSpecialization = optSpecialization
              Persistent = true }

        /// Copy a group such as when, say, you need it to be mutated with reflection but you need to preserve persistence.
        static member copy this =
            { this with GroupState.Id = this.Id }
    
    /// Hosts the ongoing state of an entity. The end-user of this engine should never touch this
    /// type, and it's public _only_ to make [<CLIMutable>] work.
    and [<CLIMutable; NoEquality; NoComparison>] EntityState =
        { Id : Guid
          Name : Name
          Xtension : Xtension
          DispatcherNp : EntityDispatcher
          CreationTimeStampNp : int64 // just needed for ordering writes to reduce diff volumes
          OptSpecialization : string option
          OptOverlayName : string option
          Position : Vector2 // NOTE: will become a Vector3 if Nu gets 3d capabilities
          Size : Vector2 // NOTE: will become a Vector3 if Nu gets 3d capabilities
          Rotation : single // NOTE: will become a Vector3 if Nu gets 3d capabilities
          Depth : single // NOTE: will become part of position if Nu gets 3d capabilities
          Overflow : Vector2
          ViewType : ViewType
          Visible : bool
          Omnipresent : bool
          PublishUpdatesNp : bool
          PublishChangesNp : bool
          Persistent : bool
          FacetNames : string Set
          FacetsNp : Facet list }

        /// Get a dynamic property and its type information.
        static member getProperty entityState propertyName =
            Xtension.getProperty propertyName entityState.Xtension

        /// The dynamic look-up operator.
        static member get entityState propertyName : 'a =
            Xtension.(?) (entityState.Xtension, propertyName)

        /// The dynamic assignment operator.
        static member set entityState propertyName (value : 'a) =
            { entityState with EntityState.Xtension = Xtension.(?<-) (entityState.Xtension, propertyName, value) }

        /// Attach a dynamic property.
        static member attachProperty name value entityState =
            { entityState with EntityState.Xtension = Xtension.attachProperty name value entityState.Xtension }

        /// Detach a dynamic property.
        static member detachProperty name entityState =
            { entityState with EntityState.Xtension = Xtension.detachProperty name entityState.Xtension }
    
        /// Get an entity state's transform.
        static member getTransform this =
            { Transform.Position = this.Position
              Size = this.Size
              Rotation = this.Rotation
              Depth = this.Depth }

        /// Set an entity state's transform.
        static member setTransform (value : Transform) (this : EntityState) =
            { this with
                Position = value.Position
                Size = value.Size
                Rotation = value.Rotation
                Depth = value.Depth }

        /// Make an entity state value.
        static member make optSpecialization optName optOverlayName dispatcher =
            let (id, name) = Reflection.deriveNameAndId optName
            { Id = id
              Name = name
              Xtension = Xtension.safe
              DispatcherNp = dispatcher
              CreationTimeStampNp = Core.getTimeStamp ()
              OptSpecialization = optSpecialization
              OptOverlayName = optOverlayName
              Position = Vector2.Zero
              Size = Constants.Engine.DefaultEntitySize
              Rotation = 0.0f
              Depth = 0.0f
              Overflow = Vector2.Zero
              ViewType = Relative
              Visible = true
              Omnipresent = false
              PublishUpdatesNp = false
              PublishChangesNp = false
              Persistent = true
              FacetNames = Set.empty
              FacetsNp = [] }

        /// Copy an entity such as when, say, you need it to be mutated with reflection but you need to preserve persistence.
        static member copy this =
            { this with EntityState.Id = this.Id }
    
    /// The game type that hosts the various screens used to navigate through a game.
    and [<StructuralEquality; NoComparison>] Game =
        { GameAddress : Game Address }
    
        interface Simulant with
            member this.ParticipantAddress = atoa<Game, Participant> this.GameAddress
            member this.SimulantAddress = atoa<Game, Simulant> this.GameAddress
            member this.GetPublishingPriority _ _ = Constants.Engine.GamePublishingPriority
            end
    
        /// View as address string.
        override this.ToString () = scstring this.GameAddress
    
        /// Get the full name of a game proxy.
        member this.GameFullName = Address.getFullName this.GameAddress
    
        /// Get the latest value of a game's properties.
        [<DebuggerBrowsable (DebuggerBrowsableState.RootHidden)>]
        member private this.View = Debug.World.viewGame Debug.World.Chosen
    
        /// Create a Game proxy from an address.
        static member proxy address = { GameAddress = address }
    
        /// Concatenate two addresses, taking the type of first address.
        static member acatf<'a> (address : 'a Address) (game : Game) = acatf address (atooa game.GameAddress)
        
        /// Concatenate two addresses, forcing the type of first address.
        static member acatff<'a> (address : 'a Address) (entity : Entity) = acatff address entity.EntityAddress
    
        /// Concatenate two addresses, taking the type of first address.
        static member (->-) (address, game) = Game.acatf address game
    
        /// Concatenate two addresses, forcing the type of first address.
        static member (->>-) (address, game) = acatff address game
    
    /// The screen type that allows transitioning to and from other screens, and also hosts the
    /// currently interactive groups of entities.
    and [<StructuralEquality; NoComparison>] Screen =
        { ScreenAddress : Screen Address }
    
        interface Simulant with
            member this.ParticipantAddress = atoa<Screen, Participant> this.ScreenAddress
            member this.SimulantAddress = atoa<Screen, Simulant> this.ScreenAddress
            member this.GetPublishingPriority _ _ = Constants.Engine.ScreenPublishingPriority
            end
    
        /// View as address string.
        override this.ToString () = scstring this.ScreenAddress
    
        /// Get the full name of a screen proxy.
        member this.ScreenFullName = Address.getFullName this.ScreenAddress
    
        /// Get the name of a screen proxy.
        member this.ScreenName = Address.getName this.ScreenAddress
    
        /// Get the latest value of a screen's properties.
        [<DebuggerBrowsable (DebuggerBrowsableState.RootHidden)>]
        member private this.View = Debug.World.viewScreen (this :> obj) Debug.World.Chosen
    
        /// Create a Screen proxy from an address.
        static member proxy address = { ScreenAddress = address }
    
        /// Concatenate two addresses, taking the type of first address.
        static member acatf<'a> (address : 'a Address) (screen : Screen) = acatf address (atooa screen.ScreenAddress)
        
        /// Concatenate two addresses, forcing the type of first address.
        static member acatff<'a> (address : 'a Address) (screen : Entity) = acatff address screen.EntityAddress
    
        /// Concatenate two addresses, taking the type of first address.
        static member (->-) (address, screen) = Screen.acatf address screen
    
        /// Concatenate two addresses, forcing the type of first address.
        static member (->>-) (address, screen) = acatff address screen
    
        /// Convert a name string to a screen's proxy.
        static member (!>) screenNameStr = Screen.proxy ^ ntoa !!screenNameStr
    
        /// Convert a screen's proxy to a group's by appending the group's name at the end.
        static member (=>) (screen, groupName) = Group.proxy ^ atoa<Screen, Group> screen.ScreenAddress ->- ntoa groupName
    
        /// Convert a screen's proxy to a group's by appending the group's name at the end.
        static member (=>) (screen : Screen, groupNameStr) = screen => !!groupNameStr
    
    /// Forms a logical group of entities.
    and [<StructuralEquality; NoComparison>] Group =
        { GroupAddress : Group Address }
    
        interface Simulant with
            member this.ParticipantAddress = atoa<Group, Participant> this.GroupAddress
            member this.SimulantAddress = atoa<Group, Simulant> this.GroupAddress
            member this.GetPublishingPriority _ _ = Constants.Engine.GroupPublishingPriority
            end
    
        /// View as address string.
        override this.ToString () = scstring this.GroupAddress
    
        /// Get the full name of a group proxy.
        member this.GroupFullName = Address.getFullName this.GroupAddress
    
        /// Get the name of a group proxy.
        member this.GroupName = Address.getName this.GroupAddress
    
        /// Get the latest value of a group's properties.
        [<DebuggerBrowsable (DebuggerBrowsableState.RootHidden)>]
        member private this.View = Debug.World.viewGroup (this :> obj) Debug.World.Chosen
    
        /// Create a Group proxy from an address.
        static member proxy address = { GroupAddress = address }
    
        /// Concatenate two addresses, taking the type of first address.
        static member acatf<'a> (address : 'a Address) (group : Group) = acatf address (atooa group.GroupAddress)
        
        /// Concatenate two addresses, forcing the type of first address.
        static member acatff<'a> (address : 'a Address) (group : Entity) = acatff address group.EntityAddress
    
        /// Convert a group's proxy to its screen's.
        static member (!<) group = Screen.proxy ^ Address.allButLast group.GroupAddress
    
        /// Convert a group's proxy to an entity's by appending the entity's name at the end.
        static member (=>) (group, entityName) = Entity.proxy ^ atoa<Group, Entity> group.GroupAddress ->- ntoa entityName
    
        /// Convert a group's proxy to an entity's by appending the entity's name at the end.
        static member (=>) (group : Group, entityNameStr) = group => !!entityNameStr
    
        /// Concatenate two addresses, taking the type of first address.
        static member (->-) (address, group) = Group.acatf address group
    
        /// Concatenate two addresses, forcing the type of first address.
        static member (->>-) (address, group) = acatff address group
    
    /// The type around which the whole game engine is based! Used in combination with dispatchers
    /// to implement things like buttons, characters, blocks, and things of that sort.
    /// OPTIMIZATION: Includes pre-constructed entity change and update event address to avoid
    /// reconstructing new ones for each entity every frame.
    and [<StructuralEquality; NoComparison>] Entity =
        { EntityAddress : Entity Address
          ChangeAddress : ParticipantChangeData<Entity, World> Address
          UpdateAddress : unit Address }
    
        interface Simulant with
            member this.ParticipantAddress = atoa<Entity, Participant> this.EntityAddress
            member this.SimulantAddress = atoa<Entity, Simulant> this.EntityAddress
            member this.GetPublishingPriority getEntityPublishingPriority world = getEntityPublishingPriority this world
            end
    
        /// View as address string.
        override this.ToString () = scstring this.EntityAddress
    
        /// Get the name of an entity proxy.
        member this.EntityFullName = Address.getFullName this.EntityAddress
    
        /// Get the name of an entity proxy.
        member this.EntityName = Address.getName this.EntityAddress
    
        /// Get the latest value of an entity's properties.
        [<DebuggerBrowsable (DebuggerBrowsableState.RootHidden)>]
        member private this.View = Debug.World.viewEntity (this :> obj) Debug.World.Chosen
    
        /// Create an Entity proxy from an address.
        static member proxy address =
            { EntityAddress = address
              ChangeAddress = ltoa [!!"Entity"; !!"Change"] ->>- address
              UpdateAddress = ntoa !!"Update" ->>- address }
    
        /// Concatenate two addresses, taking the type of first address.
        static member acatf<'a> (address : 'a Address) (entity : Entity) = acatf address (atooa entity.EntityAddress)
        
        /// Concatenate two addresses, forcing the type of first address.
        static member acatff<'a> (address : 'a Address) (entity : Entity) = acatff address entity.EntityAddress
    
        /// Convert an entity's proxy to its group's.
        static member (!<) entity = Group.proxy ^ Address.allButLast entity.EntityAddress
    
        /// Concatenate two addresses, taking the type of first address.
        static member (->-) (address, entity) = Entity.acatf address entity
    
        /// Concatenate two addresses, forcing the type of first address.
        static member (->>-) (address, entity) = acatff address entity
    
    /// The world's dispatchers (including facets).
    /// 
    /// I would prefer this type to be inlined in World, but it has been extracted to its own white-box
    /// type for efficiency reasons.
    and [<ReferenceEquality>] internal Dispatchers =
        { GameDispatchers : Map<string, GameDispatcher>
          ScreenDispatchers : Map<string, ScreenDispatcher>
          GroupDispatchers : Map<string, GroupDispatcher>
          EntityDispatchers : Map<string, EntityDispatcher>
          Facets : Map<string, Facet>
          RebuildEntityTree : Screen -> World -> Entity QuadTree }
    
    /// The world, in a functional programming sense. Hosts the game object, the dependencies needed
    /// to implement a game, messages to by consumed by the various engine sub-systems, and general
    /// configuration data.
    ///
    /// For efficiency, this type is kept under 64 bytes on 32-bit machines as to not exceed the size
    /// of a typical cache line.
    and [<ReferenceEquality>] World =
        private
            { EventSystem : World EventSystem
              Dispatchers : Dispatchers
              Subsystems : World Subsystems
              OptEntityCache : KeyedCache<Entity Address * World, EntityState option>
              ScreenDirectory : Vmap<Name, Screen Address * Vmap<Name, Group Address * Vmap<Name, Entity Address>>>
              AmbientState : World AmbientState
              GameState : GameState
              ScreenStates : Vmap<Screen Address, ScreenState>
              GroupStates : Vmap<Group Address, GroupState>
              EntityStates : Vmap<Entity Address, EntityState> }

        interface World EventWorld with
            member this.GetLiveness () = AmbientState.getLiveness this.AmbientState
            member this.GetEventSystem () = this.EventSystem
            member this.GetEmptyParticipant () = Game.proxy Address.empty :> Participant
            member this.UpdateEventSystem updater = { this with EventSystem = updater this.EventSystem }
            member this.ContainsParticipant participant =
                match participant with
                | :? Game -> true
                | :? Screen as screen -> Vmap.containsKey screen.ScreenAddress this.ScreenStates
                | :? Group as group -> Vmap.containsKey group.GroupAddress this.GroupStates
                | :? Entity as entity -> Vmap.containsKey entity.EntityAddress this.EntityStates
                | _ -> failwithumf ()
            member this.PublishEvent (participant : Participant) publisher eventData eventAddress eventTrace subscription world = 
                match Address.getNames participant.ParticipantAddress with
                | [] -> EventWorld.publishEvent<'a, 'p, Game, World> participant publisher eventData eventAddress eventTrace subscription world
                | [_] -> EventWorld.publishEvent<'a, 'p, Screen, World> participant publisher eventData eventAddress eventTrace subscription world
                | [_; _] -> EventWorld.publishEvent<'a, 'p, Group, World> participant publisher eventData eventAddress eventTrace subscription world
                | [_; _; _] -> EventWorld.publishEvent<'a, 'p, Entity, World> participant publisher eventData eventAddress eventTrace subscription world
                | _ -> failwithumf ()

        (* Debug *)

        /// Choose a world to be used for debugging. Call this whenever the most recently constructed
        /// world value is to be discarded in favor of the given world value.
        static member choose (world : World) =
#if DEBUG
            Debug.World.Chosen <- world :> obj
#endif
            world

        (* EntityTree *)

        /// Rebuild the entity tree if needed.
        static member internal rebuildEntityTree screen world =
            world.Dispatchers.RebuildEntityTree screen world

        (* EventSystem *)

        /// Get event subscriptions.
        static member getSubscriptions world =
            EventWorld.getSubscriptions<World> world

        /// Get event unsubscriptions.
        static member getUnsubscriptions world =
            EventWorld.getUnsubscriptions<World> world

        /// Add event state to the world.
        static member addEventState key state world =
            EventWorld.addEventState<'a, World> key state world

        /// Remove event state from the world.
        static member removeEventState key world =
            EventWorld.removeEventState<World> key world

        /// Get event state from the world.
        static member getEventState<'a> key world =
            EventWorld.getEventState<'a, World> key world

        /// Get whether events are being traced.
        static member getEventTracing world =
            EventWorld.getEventTracing world

        /// Set whether events are being traced.
        static member setEventTracing tracing world =
            EventWorld.setEventTracing tracing world

        /// Get the state of the event filter.
        static member getEventFilter world =
            EventWorld.getEventFilter world

        /// Set the state of the event filter.
        static member setEventFilter filter world =
            EventWorld.setEventFilter filter world

        /// TODO: document.
        static member getSubscriptionsSorted (publishSorter : SubscriptionSorter<World>) eventAddress world =
            EventWorld.getSubscriptionsSorted publishSorter eventAddress world

        /// TODO: document.
        static member getSubscriptionsSorted3 (publishSorter : SubscriptionSorter<World>) eventAddress world =
            EventWorld.getSubscriptionsSorted3 publishSorter eventAddress world

        /// Sort subscriptions using categorization via the 'by' procedure.
        static member sortSubscriptionsBy by (subscriptions : SubscriptionEntry list) (world : World) =
            EventWorld.sortSubscriptionsBy by subscriptions world

        /// Sort subscriptions by their place in the world's simulant hierarchy.
        static member sortSubscriptionsByHierarchy subscriptions world =
            World.sortSubscriptionsBy
                (fun _ _ -> Constants.Engine.EntityPublishingPriority)
                subscriptions
                world

        /// A 'no-op' for subscription sorting - that is, performs no sorting at all.
        static member sortSubscriptionsNone (subscriptions : SubscriptionEntry list) (world : World) =
            EventWorld.sortSubscriptionsNone subscriptions world

        /// Publish an event, using the given getSubscriptions and publishSorter procedures to arrange the order to which subscriptions are published.
        static member publish7<'a, 'p when 'p :> Simulant> getSubscriptions publishSorter (eventData : 'a) (eventAddress : 'a Address) eventTrace (publisher : 'p) world =
            EventWorld.publish7<'a, 'p, World> getSubscriptions publishSorter eventData eventAddress eventTrace publisher world

        /// Publish an event, using the given publishSorter procedure to arrange the order to which subscriptions are published.
        static member publish6<'a, 'p when 'p :> Simulant> publishSorter (eventData : 'a) (eventAddress : 'a Address) eventTrace (publisher : 'p) world =
            EventWorld.publish6<'a, 'p, World> publishSorter eventData eventAddress eventTrace publisher world

        /// Publish an event.
        static member publish<'a, 'p when 'p :> Simulant>
            (eventData : 'a) (eventAddress : 'a Address) eventTrace (publisher : 'p) world =
            EventWorld.publish6<'a, 'p, World> World.sortSubscriptionsByHierarchy eventData eventAddress eventTrace publisher world

        /// Unsubscribe from an event.
        static member unsubscribe subscriptionKey world =
            EventWorld.unsubscribe<World> subscriptionKey world

        /// Subscribe to an event using the given subscriptionKey, and be provided with an unsubscription callback.
        static member subscribePlus5<'a, 's when 's :> Simulant>
            subscriptionKey (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.subscribePlus5<'a, 's, World> subscriptionKey subscription eventAddress subscriber world

        /// Subscribe to an event, and be provided with an unsubscription callback.
        static member subscribePlus<'a, 's when 's :> Simulant>
            (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.subscribePlus<'a, 's, World> subscription eventAddress subscriber world

        /// Subscribe to an event using the given subscriptionKey.
        static member subscribe5<'a, 's when 's :> Simulant>
            subscriptionKey (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.subscribe5<'a, 's, World> subscriptionKey subscription eventAddress subscriber world

        /// Subscribe to an event.
        static member subscribe<'a, 's when 's :> Simulant>
            (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.subscribe<'a, 's, World> subscription eventAddress subscriber world

        /// Keep active a subscription for the lifetime of a simulant, and be provided with an unsubscription callback.
        static member monitorPlus<'a, 's when 's :> Simulant>
            (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.monitorPlus<'a, 's, World> subscription eventAddress subscriber world

        /// Keep active a subscription for the lifetime of a simulant.
        static member monitor<'a, 's when 's :> Simulant>
            (subscription : Subscription<'a, 's, World>) (eventAddress : 'a Address) (subscriber : 's) world =
            EventWorld.monitor<'a, 's, World> subscription eventAddress subscriber world

        (* Dispatchers *)

        /// Get the game dispatchers of the world.
        static member getGameDispatchers world =
            world.Dispatchers.GameDispatchers

        /// Get the screen dispatchers of the world.
        static member getScreenDispatchers world =
            world.Dispatchers.ScreenDispatchers

        /// Get the group dispatchers of the world.
        static member getGroupDispatchers world =
            world.Dispatchers.GroupDispatchers

        /// Get the entity dispatchers of the world.
        static member getEntityDispatchers world =
            world.Dispatchers.EntityDispatchers

        /// Get the facets of the world.
        static member getFacets world =
            world.Dispatchers.Facets

        (* Subsystems *)

        static member internal getSubsystemMap world =
            Subsystems.getSubsystemMap world.Subsystems

        static member internal getSubsystem<'s when 's :> World Subsystem> name world : 's =
            Subsystems.getSubsystem name world.Subsystems

        static member internal getSubsystemBy<'s, 't when 's :> World Subsystem> (by : 's -> 't) name world : 't =
            Subsystems.getSubsystemBy by name world.Subsystems

        // NOTE: it'd be nice to get rid of this function to improve encapsulation, but I can't seem to do so in practice...
        static member internal setSubsystem<'s when 's :> World Subsystem> (subsystem : 's) name world =
            World.choose { world with Subsystems = Subsystems.setSubsystem subsystem name world.Subsystems }

        static member internal updateSubsystem<'s when 's :> World Subsystem> (updater : 's -> World -> 's) name world =
            World.choose { world with Subsystems = Subsystems.updateSubsystem updater name world.Subsystems world }

        static member internal updateSubsystems (updater : World Subsystem -> World -> World Subsystem) world =
            World.choose { world with Subsystems = Subsystems.updateSubsystems updater world.Subsystems world }

        static member internal clearSubsystemsMessages world =
            World.choose { world with Subsystems = Subsystems.clearSubsystemsMessages world.Subsystems world }

        (* AmbientState *)

        static member internal getAmbientState world =
            world.AmbientState

        static member internal getAmbientStateBy by world =
            by world.AmbientState

        static member internal updateAmbientState updater world =
            World.choose { world with AmbientState = updater world.AmbientState }

        static member internal updateAmbientStateWithoutEvent updater world =
            let _ = world
            let world = World.choose { world with AmbientState = updater world.AmbientState }
            let _ = EventTrace.record "World" "updateAmbientState" EventTrace.empty
            world

        (* OptEntityCache *)

        /// Get the opt entity cache.
        static member internal getOptEntityCache world =
            world.OptEntityCache

        /// Get the opt entity cache.
        static member internal setOptEntityCache optEntityCache world =
            World.choose { world with OptEntityCache = optEntityCache }

        (* ScreenDirectory *)

        /// Get the opt entity cache.
        static member internal getScreenDirectory world =
            world.ScreenDirectory

        (* EntityState *)

        static member private optEntityStateKeyEquality 
            (entityAddress : Entity Address, world : World)
            (entityAddress2 : Entity Address, world2 : World) =
            refEq entityAddress entityAddress2 && refEq world world2

        static member private optEntityGetFreshKeyAndValue entity world =
            let optEntityState = Vmap.tryFind entity.EntityAddress ^ world.EntityStates
            ((entity.EntityAddress, world), optEntityState)

        static member private optEntityStateFinder entity world =
            KeyedCache.getValue
                World.optEntityStateKeyEquality
                (fun () -> World.optEntityGetFreshKeyAndValue entity world)
                (entity.EntityAddress, world)
                (World.getOptEntityCache world)

        static member private entityStateSetter entityState entity world =
#if DEBUG
            if not ^ Vmap.containsKey entity.EntityAddress world.EntityStates then
                failwith ^ "Cannot set the state of a non-existent entity '" + scstring entity.EntityAddress + "'"
#endif
            let entityStates = Vmap.add entity.EntityAddress entityState world.EntityStates
            World.choose { world with EntityStates = entityStates }

        static member private entityStateAdder entityState entity world =
            let screenDirectory =
                match Address.getNames entity.EntityAddress with
                | [screenName; groupName; entityName] ->
                    match Vmap.tryFind screenName world.ScreenDirectory with
                    | Some (screenAddress, groupDirectory) ->
                        match Vmap.tryFind groupName groupDirectory with
                        | Some (groupAddress, entityDirectory) ->
                            let entityDirectory = Vmap.add entityName entity.EntityAddress entityDirectory
                            let groupDirectory = Vmap.add groupName (groupAddress, entityDirectory) groupDirectory
                            Vmap.add screenName (screenAddress, groupDirectory) world.ScreenDirectory
                        | None -> failwith ^ "Cannot add entity '" + scstring entity.EntityAddress + "' to non-existent group."
                    | None -> failwith ^ "Cannot add entity '" + scstring entity.EntityAddress + "' to non-existent screen."
                | _ -> failwith ^ "Invalid entity address '" + scstring entity.EntityAddress + "'."
            let entityStates = Vmap.add entity.EntityAddress entityState world.EntityStates
            World.choose { world with ScreenDirectory = screenDirectory; EntityStates = entityStates }

        static member private entityStateRemover entity world =
            let screenDirectory =
                match Address.getNames entity.EntityAddress with
                | [screenName; groupName; entityName] ->
                    match Vmap.tryFind screenName world.ScreenDirectory with
                    | Some (screenAddress, groupDirectory) ->
                        match Vmap.tryFind groupName groupDirectory with
                        | Some (groupAddress, entityDirectory) ->
                            let entityDirectory = Vmap.remove entityName entityDirectory
                            let groupDirectory = Vmap.add groupName (groupAddress, entityDirectory) groupDirectory
                            Vmap.add screenName (screenAddress, groupDirectory) world.ScreenDirectory
                        | None -> failwith ^ "Cannot remove entity '" + scstring entity.EntityAddress + "' from non-existent group."
                    | None -> failwith ^ "Cannot remove entity '" + scstring entity.EntityAddress + "' from non-existent screen."
                | _ -> failwith ^ "Invalid entity address '" + scstring entity.EntityAddress + "'."
            let entityStates = Vmap.remove entity.EntityAddress world.EntityStates
            World.choose { world with ScreenDirectory = screenDirectory; EntityStates = entityStates }

        static member internal getEntityStateBoundsMax entityState =
            // TODO: get up off yer arse and write an algorithm for tight-fitting bounds...
            match entityState.Rotation with
            | 0.0f ->
                let boundsOverflow = Math.makeBoundsOverflow entityState.Position entityState.Size entityState.Overflow
                boundsOverflow // no need to transform is unrotated
            | _ ->
                let boundsOverflow = Math.makeBoundsOverflow entityState.Position entityState.Size entityState.Overflow
                let position = boundsOverflow.Xy
                let size = Vector2 (boundsOverflow.Z, boundsOverflow.W) - position
                let center = position + size * 0.5f
                let corner = position + size
                let centerToCorner = corner - center
                let quaternion = Quaternion.FromAxisAngle (Vector3.UnitZ, Constants.Math.DegreesToRadiansF * 45.0f)
                let newSizeOver2 = Vector2 (Vector2.Transform (centerToCorner, quaternion)).Y
                let newPosition = center - newSizeOver2
                let newSize = newSizeOver2 * 2.0f
                Vector4 (newPosition.X, newPosition.Y, newPosition.X + newSize.X, newPosition.Y + newSize.Y)

        static member internal publishEntityChange entityState (entity : Entity) oldWorld world =
            if entityState.PublishChangesNp then
                let eventTrace = EventTrace.record "World" "publishEntityChange" EventTrace.empty
                World.publish { Participant = entity; OldWorld = oldWorld } entity.ChangeAddress eventTrace entity world
            else world

        static member inline internal getOptEntityState entity world =
            World.optEntityStateFinder entity world

        static member internal getEntityState entity world =
            match World.getOptEntityState entity world with
            | Some entityState -> entityState
            | None -> failwith ^ "Could not find entity with address '" + scstring entity.EntityAddress + "'."

        static member internal addEntityState entityState entity world =
            World.entityStateAdder entityState entity world

        static member internal removeEntityState entity world =
            World.entityStateRemover entity world

        static member inline internal setEntityStateWithoutEvent entityState entity world =
            World.entityStateSetter entityState entity world

        static member internal setEntityState entityState (entity : Entity) world =
            let oldWorld = world
            let world = World.entityStateSetter entityState entity world
            World.publishEntityChange entityState entity oldWorld world

        static member internal updateEntityStateWithoutEvent updater entity world =
            let entityState = World.getEntityState entity world
            let entityState = updater entityState
            World.setEntityStateWithoutEvent entityState entity world

        static member internal updateEntityState updater entity world =
            let entityState = World.getEntityState entity world
            let entityState = updater entityState
            World.setEntityState entityState entity world

        static member getEntityBoundsMax entity world =
            let entityState = World.getEntityState entity world
            World.getEntityStateBoundsMax entityState

        (* GroupState *)

        static member private groupStateSetter groupState group world =
#if DEBUG
            if not ^ Vmap.containsKey group.GroupAddress world.GroupStates then
                failwith ^ "Cannot set the state of a non-existent group '" + scstring group.GroupAddress + "'"
#endif
            let groupStates = Vmap.add group.GroupAddress groupState world.GroupStates
            World.choose { world with GroupStates = groupStates }

        static member private groupStateAdder groupState group world =
            let screenDirectory =
                match Address.getNames group.GroupAddress with
                | [screenName; groupName] ->
                    match Vmap.tryFind screenName world.ScreenDirectory with
                    | Some (screenAddress, groupDirectory) ->
                        match Vmap.tryFind groupName groupDirectory with
                        | Some (groupAddress, entityDirectory) ->
                            let groupDirectory = Vmap.add groupName (groupAddress, entityDirectory) groupDirectory
                            Vmap.add screenName (screenAddress, groupDirectory) world.ScreenDirectory
                        | None ->
                            let entityDirectory = Vmap.makeEmpty ()
                            let groupDirectory = Vmap.add groupName (group.GroupAddress, entityDirectory) groupDirectory
                            Vmap.add screenName (screenAddress, groupDirectory) world.ScreenDirectory
                    | None -> failwith ^ "Cannot add group '" + scstring group.GroupAddress + "' to non-existent screen."
                | _ -> failwith ^ "Invalid group address '" + scstring group.GroupAddress + "'."
            let groupStates = Vmap.add group.GroupAddress groupState world.GroupStates
            World.choose { world with ScreenDirectory = screenDirectory; GroupStates = groupStates }

        static member private groupStateRemover group world =
            let screenDirectory =
                match Address.getNames group.GroupAddress with
                | [screenName; groupName] ->
                    match Vmap.tryFind screenName world.ScreenDirectory with
                    | Some (screenAddress, groupDirectory) ->
                        let groupDirectory = Vmap.remove groupName groupDirectory
                        Vmap.add screenName (screenAddress, groupDirectory) world.ScreenDirectory
                    | None -> failwith ^ "Cannot remove group '" + scstring group.GroupAddress + "' from non-existent screen."
                | _ -> failwith ^ "Invalid group address '" + scstring group.GroupAddress + "'."
            let groupStates = Vmap.remove group.GroupAddress world.GroupStates
            World.choose { world with ScreenDirectory = screenDirectory; GroupStates = groupStates }

        static member inline internal getOptGroupState group world =
            Vmap.tryFind group.GroupAddress world.GroupStates

        static member internal getGroupState group world =
            match World.getOptGroupState group world with
            | Some groupState -> groupState
            | None -> failwith ^ "Could not find group with address '" + scstring group.GroupAddress + "'."

        static member internal addGroupState groupState group world =
            World.groupStateAdder groupState group world

        static member internal removeGroupState group world =
            World.groupStateRemover group world

        static member inline internal setGroupStateWithoutEvent groupState group world =
            World.groupStateSetter groupState group world

        static member internal setGroupState groupState group world =
            let _ = world
            let world = World.groupStateSetter groupState group world
            let _ = EventTrace.record "World" "setGroupState" EventTrace.empty
            world

        static member internal updateGroupState updater group world =
            let groupState = World.getGroupState group world
            let groupState = updater groupState
            World.setGroupState groupState group world

        (* ScreenState *)

        static member private screenStateSetter screenState screen world =
#if DEBUG
            if not ^ Vmap.containsKey screen.ScreenAddress world.ScreenStates then
                failwith ^ "Cannot set the state of a non-existent screen '" + scstring screen.ScreenAddress + "'"
#endif
            let screenStates = Vmap.add screen.ScreenAddress screenState world.ScreenStates
            World.choose { world with ScreenStates = screenStates }

        static member private screenStateAdder screenState screen world =
            let screenDirectory =
                match Address.getNames screen.ScreenAddress with
                | [screenName] ->
                    match Vmap.tryFind screenName world.ScreenDirectory with
                    | Some (_, groupDirectory) ->
                        // NOTE: this is logically a redundant operation...
                        Vmap.add screenName (screen.ScreenAddress, groupDirectory) world.ScreenDirectory
                    | None ->
                        let groupDirectory = Vmap.makeEmpty ()
                        Vmap.add screenName (screen.ScreenAddress, groupDirectory) world.ScreenDirectory
                | _ -> failwith ^ "Invalid screen address '" + scstring screen.ScreenAddress + "'."
            let screenStates = Vmap.add screen.ScreenAddress screenState world.ScreenStates
            World.choose { world with ScreenDirectory = screenDirectory; ScreenStates = screenStates }

        static member private screenStateRemover screen world =
            let screenDirectory =
                match Address.getNames screen.ScreenAddress with
                | [screenName] -> Vmap.remove screenName world.ScreenDirectory
                | _ -> failwith ^ "Invalid screen address '" + scstring screen.ScreenAddress + "'."
            let screenStates = Vmap.remove screen.ScreenAddress world.ScreenStates
            World.choose { world with ScreenDirectory = screenDirectory; ScreenStates = screenStates }

        static member inline internal getOptScreenState screen world =
            Vmap.tryFind screen.ScreenAddress world.ScreenStates

        static member internal getScreenState screen world =
            match World.getOptScreenState screen world with
            | Some screenState -> screenState
            | None -> failwith ^ "Could not find screen with address '" + scstring screen.ScreenAddress + "'."

        static member internal addScreenState screenState screen world =
            World.screenStateAdder screenState screen world

        static member internal removeScreenState screen world =
            World.screenStateRemover screen world

        static member inline internal setScreenStateWithoutEvent screenState screen world =
            World.screenStateSetter screenState screen world

        static member internal setScreenState screenState screen world =
            let _ = world
            let world = World.screenStateSetter screenState screen world
            let _ = EventTrace.record "World" "setScreenState" EventTrace.empty
            world

        static member internal updateScreenState updater screen world =
            let screenState = World.getScreenState screen world
            let screenState = updater screenState
            World.setScreenState screenState screen world

        static member internal updateEntityInEntityTree entity oldWorld world =

            // OPTIMIZATION: attempt to avoid constructing a screen address on each call to decrease address hashing
            // OPTIMIZATION: assumes a valid entity address with List.head on its names
            let screen =
                match (World.getGameState world).OptSelectedScreen with
                | Some screen when screen.ScreenName = List.head ^ Address.getNames entity.EntityAddress -> screen
                | Some _ | None -> entity.EntityAddress |> Address.getNames |> List.head |> ntoa<Screen> |> Screen.proxy

            // proceed with updating entity in entity tree
            let screenState = World.getScreenState screen world
            let entityTree =
                MutantCache.mutateMutant
                    (fun () -> World.rebuildEntityTree screen oldWorld)
                    (fun entityTree ->
                        let oldEntityState = World.getEntityState entity oldWorld
                        let oldEntityBoundsMax = World.getEntityStateBoundsMax oldEntityState
                        let entityState = World.getEntityState entity world
                        let entityBoundsMax = World.getEntityStateBoundsMax entityState
                        QuadTree.updateElement
                            (oldEntityState.Omnipresent || oldEntityState.ViewType = Absolute) oldEntityBoundsMax
                            (entityState.Omnipresent || entityState.ViewType = Absolute) entityBoundsMax
                            entity entityTree
                        entityTree)
                    screenState.EntityTreeNp
            let screenState = { screenState with EntityTreeNp = entityTree }
            World.setScreenStateWithoutEvent screenState screen world

        static member internal updateEntityStatePlus updater entity world =
            let oldWorld = world
            let world = World.updateEntityStateWithoutEvent updater entity world
            let world = World.updateEntityInEntityTree entity oldWorld world
            World.publishEntityChange (World.getEntityState entity world) entity oldWorld world

        (* GameState *)

        static member internal getGameState world =
            world.GameState

        static member internal setGameState gameState world =
            let _ = world
            let world = World.choose { world with GameState = gameState }
            let _ = EventTrace.record "World" "setGameState" EventTrace.empty
            world

        static member internal updateGameState updater world =
            let gameState = World.getGameState world
            let gameState = updater gameState
            World.setGameState gameState world

        /// Get the current destination screen if a screen transition is currently underway.
        static member getOptScreenTransitionDestination world =
            (World.getGameState world).OptScreenTransitionDestination

        /// Set the current destination screen on the precondition that no screen transition is currently underway.
        static member internal setOptScreenTransitionDestination destination world =
            World.updateGameState
                (fun gameState -> { gameState with OptScreenTransitionDestination = destination })
                world
                
        // Make the world.
        static member internal make eventSystem dispatchers subsystems ambientState gameState =
            let world =
                { EventSystem = eventSystem
                  Dispatchers = dispatchers
                  Subsystems = subsystems
                  OptEntityCache = Unchecked.defaultof<KeyedCache<Entity Address * World, EntityState option>>
                  ScreenDirectory = Vmap.makeEmpty ()
                  AmbientState = ambientState
                  GameState = gameState
                  ScreenStates = Vmap.makeEmpty ()
                  GroupStates = Vmap.makeEmpty ()
                  EntityStates = Vmap.makeEmpty () }
            World.choose world

/// A simulant in the world.
type Simulant = WorldTypes.Simulant

/// The data for a change in the world's ambient state.
type AmbientChangeData = WorldTypes.AmbientChangeData

/// The default dispatcher for games.
type GameDispatcher = WorldTypes.GameDispatcher

/// The default dispatcher for screens.
type ScreenDispatcher = WorldTypes.ScreenDispatcher

/// The default dispatcher for groups.
type GroupDispatcher = WorldTypes.GroupDispatcher

/// The default dispatcher for entities.
type EntityDispatcher = WorldTypes.EntityDispatcher

/// Dynamically augments an entity's behavior in a composable way.
type Facet = WorldTypes.Facet

/// The game type that hosts the various screens used to navigate through a game.
type Game = WorldTypes.Game

/// The screen type that allows transitioning to and from other screens, and also hosts the
/// currently interactive groups of entities.
type Screen = WorldTypes.Screen

/// Forms a logical group of entities.
type Group = WorldTypes.Group

/// The type around which the whole game engine is based! Used in combination with dispatchers
/// to implement things like buttons, characters, blocks, and things of that sort.
/// OPTIMIZATION: Includes pre-constructed entity change and update event address to avoid
/// reconstructing new ones for each entity every frame.
type Entity = WorldTypes.Entity

/// The world, in a functional programming sense. Hosts the game object, the dependencies needed
/// to implement a game, messages to by consumed by the various engine sub-systems, and general
/// configuration data.
///
/// For efficiency, this type is kept under 64 bytes on 32-bit machines as to not exceed the size
/// of a typical cache line.
type World = WorldTypes.World