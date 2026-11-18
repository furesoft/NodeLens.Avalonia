<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="a9038ee2-e137-49bf-97f8-71e3e5bf02ed" name="Orders">
  <nodes>
    <node id="9fd4acbf-5ca2-403b-ba63-8863f82171c1" concept="Sql.Schema" name="orders">
      <properties>
        <name>orders</name>
      </properties>
      <children role="tables">
        <node id="9d1f3d38-93d5-4723-af15-85cd9208887c" concept="Sql.Table" name="order">
          <properties>
            <name>order</name>
          </properties>
          <children role="columns">
            <node id="8b7c222a-0c51-478e-932c-70027c74f032" concept="Sql.Column" name="id">
              <properties>
                <name>id</name>
                <type>int</type>
              </properties>
            </node>
            <node id="9a3385ee-5e99-4b38-a557-064be64e2416" concept="Sql.Column" name="placed">
              <properties>
                <name>placed</name>
                <type>date</type>
              </properties>
            </node>
            <node id="2133f978-0824-40dc-831f-c45546e30e9a" concept="Sql.Column" name="person_id">
              <properties>
                <name>person_id</name>
                <type>int</type>
              </properties>
            </node>
          </children>
          <children role="keys">
            <node id="a44fbd99-03b3-40e3-add0-1166127c0cc4" concept="Sql.ForeignKey">
              <references role="local" target="2133f978-0824-40dc-831f-c45546e30e9a" resolveInfo="person_id" />
              <references role="target" target="8436ba5b-8248-45fa-9559-2bacd8e784a1" resolveInfo="id" />
            </node>
            <node id="fa98b63a-33a8-43af-90aa-796605912b93" concept="Sql.ForeignKey">
              <references role="local" target="2133f978-0824-40dc-831f-c45546e30e9a" resolveInfo="person_id" />
              <references role="target" target="8b7c222a-0c51-478e-932c-70027c74f032" resolveInfo="id" />
            </node>
          </children>
        </node>
      </children>
    </node>
  </nodes>
</model>