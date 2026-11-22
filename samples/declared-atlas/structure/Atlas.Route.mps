<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b0000000-0000-4000-9000-000000000006" name="Atlas.Route">
  <nodes>
    <node id="b0000000-0000-4000-8000-000000000006" concept="Structure.Concept" name="Atlas.Route">
      <properties>
        <key>Atlas.Route</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias>route</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>A way through a map, as the places it stops at.</description>
        <nameSlot>name</nameSlot>
      </properties>
      <children role="properties">
        <node id="b0000000-0000-4000-8000-000000000501" concept="Structure.PropertyDecl" name="name">
          <properties>
            <name>name</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="b0000000-0000-4000-8000-000000000502" concept="Structure.ChildDecl" name="stops">
          <properties>
            <name>stops</name>
            <childConceptKey>Atlas.Stop</childConceptKey>
            <cardinality>0..n</cardinality>
          </properties>
        </node>
      </children>
      <children role="references">
        <node id="b0000000-0000-4000-8000-000000000503" concept="Structure.ReferenceDecl" name="onMap">
          <properties>
            <name>onMap</name>
            <targetConceptKey>Atlas.Map</targetConceptKey>
            <allowOwnSubtree>false</allowOwnSubtree>
          </properties>
        </node>
        <node id="b0000000-0000-4000-8000-000000000504" concept="Structure.ReferenceDecl" name="start">
          <properties>
            <name>start</name>
            <targetConceptKey>Atlas.Place</targetConceptKey>
            <allowOwnSubtree>false</allowOwnSubtree>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>